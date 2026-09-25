# V6 Large-fixture API baseline

## Purpose

This slice connects the PostgreSQL query-plan evidence with real HTTP measurements
against the same disposable database fixture. It is intended to answer whether the
current read-only API paths need an index, projection, pagination, or cache change
before an optimization is implemented. The latest table is the pre-index
baseline; the candidate comparison is recorded separately below.

The test is intentionally opt-in because it starts Docker, creates a PostgreSQL
Testcontainer, inserts thousands of rows, and performs repeated authenticated HTTP
requests. It is not part of the default fast integration-test run.

## Repeatable command

Run from the repository root with Docker available:

```powershell
$env:V6_RUN_LARGE_FIXTURE_BASELINE = "true"
$env:V6_LARGE_FIXTURE_TASK_COUNT = "10000"
$env:V6_LARGE_FIXTURE_NOISE_PROJECT_COUNT = "20"
$env:V6_LARGE_FIXTURE_WARMUP_REQUESTS = "5"
$env:V6_LARGE_FIXTURE_MEASURED_REQUESTS = "30"
$env:V6_BASELINE_OUTPUT_DIRECTORY = (Join-Path (Get-Location) "artifacts\v6")

dotnet test `
  "backend\IntegrationTests\IntegrationTests.csproj" `
  --filter "FullyQualifiedName~PostgreSqlLargeFixtureApiBaselineTests" `
  -m:1
```

The output directory is optional. If it is set, the generated
`large-fixture-api-baseline-*.md` report is written there. Reports contain
ephemeral fixture identifiers and are ignored by Git.

Before the measured requests, the harness runs `ANALYZE` for the task, label and
activity tables so the plan comparison uses current PostgreSQL statistics.

The test can be scaled without changing source code:

| Variable | Default | Meaning |
|---|---:|---|
| `V6_LARGE_FIXTURE_TASK_COUNT` | `10000` | Tasks in the benchmark project |
| `V6_LARGE_FIXTURE_NOISE_PROJECT_COUNT` | `20` | Additional projects visible to the benchmark user |
| `V6_LARGE_FIXTURE_WARMUP_REQUESTS` | `5` | Sequential warmup requests per API scenario |
| `V6_LARGE_FIXTURE_MEASURED_REQUESTS` | `30` | Sequential measured requests per API scenario |

The fixture contains two labels per task and one activity per five tasks. The
benchmark user receives a short-lived JWT from the existing application token
service, so the measurement exercises the normal authentication and authorization
pipeline rather than bypassing it.

## Pre-index baseline run

The following pre-index result was captured on 2026-09-25 with 10,000 tasks, 20 noise
projects, 20,000 labels, 2,000 activities, five warmup requests, and 30 measured
requests per scenario. The host was Windows with Docker Desktop and PostgreSQL
`16-alpine`. Requests were sequential, with concurrency equal to one.

| Scenario | Success | p50 | p95 | p99 | Average | Average payload | Throughput |
|---|---:|---:|---:|---:|---:|---:|---:|
| `projects.list` | 30/30 | 7.904 ms | 11.467 ms | 19.574 ms | 8.669 ms | 7,491.9 bytes | 114.279 req/s |
| `project.tasks.page-1` | 30/30 | 21.704 ms | 33.599 ms | 37.139 ms | 22.343 ms | 9,691.9 bytes | 44.723 req/s |
| `project.dashboard` | 30/30 | 30.396 ms | 35.870 ms | 36.977 ms | 30.897 ms | 11,637.0 bytes | 32.349 req/s |

These numbers are an application baseline, not a production SLO and not a
capacity test. They are only comparable with another run that keeps the fixture,
request parameters, application version, database engine, and host resources
consistent.

## EF Core timing and generated SQL

The same run captured the actual EF Core commands issued during each measured
HTTP request. The capture is test-only: it uses a `DbCommandInterceptor` in the
PostgreSQL integration factory and does not change production DI or logging.

| Scenario | SQL commands/request | EF p50 | EF p95 | EF p99 | Non-database p50 | Non-database p95 | Non-database p99 |
|---|---:|---:|---:|---:|---:|---:|---:|
| `projects.list` | 1 | 3.285 ms | 4.359 ms | 4.580 ms | 4.778 ms | 6.887 ms | 16.308 ms |
| `project.tasks.page-1` | 3 | 15.138 ms | 16.899 ms | 18.188 ms | 6.242 ms | 17.440 ms | 21.616 ms |
| `project.dashboard` | 5 | 22.121 ms | 25.275 ms | 26.168 ms | 8.389 ms | 9.702 ms | 13.589 ms |

The command counts match the current orchestration:

- project list: one projection query;
- task page: one project-access query, one page query and one count query;
- dashboard: one access query, one task-statistics query, two due-date queries
  and one recent-activity query.

This explains an important part of the API/SQL gap. The dashboard's database
work is split across five round-trips, while the task page uses three. The
remaining time includes EF materialization, endpoint orchestration, middleware
and JSON serialization. It is not evidence by itself that a cache or a new
index is needed.

## Pre-index query-plan evidence

The same run captured `EXPLAIN (ANALYZE, BUFFERS, VERBOSE)` for the representative
task and dashboard queries:

| Query path | Execution time | Main observation |
|---|---:|---|
| Task page with labels | 7.907 ms | Uses the existing task project index, reads 10,000 project tasks, performs a top-N sort, and scans the 20,000-row label table for the join. |
| Dashboard statistics | 4.452 ms | Uses the existing `ProjectTasks(ProjectId, Status, DueDate)` index through a bitmap scan. |
| Overdue tasks | 1.736 ms | Uses the project/date portion of the composite index; `Status <> 'Done'` remains a filter. |
| Upcoming tasks | 2.165 ms | Uses the composite project/date index while filtering `Status <> 'Done'`. |
| Recent activity | 2.002 ms | Uses `ProjectActivities(ProjectId, CreatedAt)` and a small top-N sort after reading 2,000 activity rows. |

The plans did not show a production-worthy reason to add an index blindly. The
candidate partial due-date index remains only a hypothesis. A separate
before/after experiment approved the `CreatedAt` ordering index and it is now
represented by a production migration.

The API-to-plan gap is now split into captured EF command time and the remaining
HTTP time. The task-page index decision was made from a separate before/after
comparison rather than from this baseline alone.

## Post-migration repeat

The analyzed post-migration repeat was captured on 2026-09-25 with the same
fixture and request counts. All 90 requests returned HTTP 200:

| Scenario | Success | p50 | p95 | p99 | Average |
|---|---:|---:|---:|---:|---:|
| `projects.list` | 30/30 | 6.515 ms | 8.434 ms | 13.106 ms | 6.811 ms |
| `project.tasks.page-1` | 30/30 | 12.112 ms | 14.019 ms | 15.798 ms | 12.291 ms |
| `project.dashboard` | 30/30 | 26.203 ms | 29.267 ms | 30.929 ms | 26.225 ms |

The representative task-page plan used
`IX_ProjectTasks_ProjectId_CreatedAt`, read 20 task rows in index order and
completed in `0.258 ms`. This repeat is a post-migration verification, not a
strict SLO comparison against the earlier run because host scheduling and
container state can vary; the same-run candidate experiment remains the
decision evidence.

## Next decision gate

For another index or projection:

1. compare the generated EF SQL with the hand-written representative plans;
2. define a measurable threshold for a candidate reduction in round-trips or p95;
3. run a before/after comparison with the same 10,000-task fixture;
4. keep the change only if it improves the target p95 without increasing error
   rate or causing a worse plan for the other dashboard paths.

## Candidate index evidence

`PostgreSqlTaskPaginationCandidateTests` measured the same task-page endpoint
before and after `(ProjectId, CreatedAt DESC)` in a disposable PostgreSQL
database. The test intentionally removed the migrated index for the before
phase, then recreated it for the after phase.

| Phase | Requests | Errors | p50 | p95 | p99 |
|---|---:|---:|---:|---:|---:|
| Without candidate index | 30 | 0 | 17.821 ms | 22.272 ms | 32.656 ms |
| With candidate index | 30 | 0 | 12.927 ms | 19.060 ms | 19.894 ms |

The after plan used `IX_ProjectTasks_ProjectId_CreatedAt`, read the first 20
tasks directly in descending `CreatedAt` order, and reduced representative plan
execution from `5.114 ms` to `0.370 ms`. The migration
[`20260925163911_AddProjectTaskCreatedAtIndex`](../backend/Infrastructure/Data/Migrations/20260925163911_AddProjectTaskCreatedAtIndex.cs)
is therefore included as the first measured V6 schema optimization.

No Redis or cache was added. The label join still scans the label table in this
query shape, so a future label-side optimization requires its own evidence.
