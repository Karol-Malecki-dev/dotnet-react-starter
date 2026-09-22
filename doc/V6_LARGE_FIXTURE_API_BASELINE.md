# V6 Large-fixture API baseline

## Purpose

This slice connects the PostgreSQL query-plan evidence with real HTTP measurements
against the same disposable database fixture. It is intended to answer whether the
current read-only API paths need an index, projection, pagination, or cache change
before any optimization is implemented.

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

## Latest local run

The following result was captured with 10,000 tasks, 20 noise projects, 20,000
labels, 2,000 activities, five warmup requests, and 30 measured requests per
scenario. The host was Windows with Docker Desktop and PostgreSQL `16-alpine`.
Requests were sequential, with concurrency equal to one.

| Scenario | Success | p50 | p95 | p99 | Average | Average payload | Throughput |
|---|---:|---:|---:|---:|---:|---:|---:|
| `projects.list` | 30/30 | 8.644 ms | 17.333 ms | 32.345 ms | 9.504 ms | 7,493.8 bytes | 105.035 req/s |
| `project.tasks.page-1` | 30/30 | 25.846 ms | 30.022 ms | 30.163 ms | 26.055 ms | 9,689.9 bytes | 38.358 req/s |
| `project.dashboard` | 30/30 | 31.553 ms | 39.321 ms | 39.990 ms | 32.505 ms | 11,638.8 bytes | 30.752 req/s |

These numbers are an application baseline, not a production SLO and not a
capacity test. They are only comparable with another run that keeps the fixture,
request parameters, application version, database engine, and host resources
consistent.

## Query-plan evidence

The same run captured `EXPLAIN (ANALYZE, BUFFERS, VERBOSE)` for the representative
task and dashboard queries:

| Query path | Execution time | Main observation |
|---|---:|---|
| Task page with labels | 7.589 ms | Uses the existing task project index, reads 10,000 project tasks, performs a top-N sort, and scans the 20,000-row label table for the join. |
| Dashboard statistics | 3.946 ms | Uses the existing `ProjectTasks(ProjectId, Status, DueDate)` index through a bitmap scan. |
| Overdue tasks | 1.417 ms | Uses the project/date portion of the composite index; `Status <> 'Done'` remains a filter and removed 276 rows in this fixture. |
| Upcoming tasks | 1.711 ms | Uses the composite project/date index and removed 296 rows by the non-sargable status filter. |
| Recent activity | 1.960 ms | Uses `ProjectActivities(ProjectId, CreatedAt)` and a small top-N sort after reading 2,000 activity rows. |

The plans do not currently show a production-worthy reason to add an index
blindly. In particular, the candidate partial due-date index and a
`CreatedAt` ordering index remain hypotheses, not migrations.

The gap between the multi-query dashboard API p95 and the individual database
plan times also means that the next investigation should separate database time
from application materialization and endpoint orchestration time before introducing
cache or changing the schema.

## Next decision gate

Before adding an index or projection:

1. repeat the baseline on the same fixture after a clean container start;
2. capture the generated EF SQL for the API endpoints, not only representative
   hand-written plans;
3. define a measurable threshold for the candidate change;
4. run a before/after comparison with the same task count and request mix;
5. keep the change only if it improves the target p95 without increasing error
   rate or causing a worse plan for the other dashboard paths.

No Redis, cache, speculative migration, or production query change is included in
this slice.
