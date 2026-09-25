# V6 EF SQL timing

## Purpose

The large-fixture baseline showed that individual PostgreSQL plans were faster
than the complete HTTP endpoints. This diagnostic slice measures the missing
middle layer without changing production behavior:

```text
HTTP request
    |
    +-- endpoint orchestration
    +-- EF Core materialization
    +-- PostgreSQL commands
    +-- response serialization
```

The PostgreSQL integration factory registers
[`EfCommandCapture`](../backend/IntegrationTests/EfCommandCapture.cs) only in
the test host. It records command kind, generated SQL text, and EF-reported
command duration. Parameter values are not recorded.

## Repeatable run

Docker Desktop must be running:

```powershell
$env:V6_RUN_LARGE_FIXTURE_BASELINE = "true"
$env:V6_LARGE_FIXTURE_TASK_COUNT = "10000"
$env:V6_LARGE_FIXTURE_WARMUP_REQUESTS = "5"
$env:V6_LARGE_FIXTURE_MEASURED_REQUESTS = "30"
$env:V6_BASELINE_OUTPUT_DIRECTORY = (Join-Path (Get-Location) "artifacts\v6")

dotnet test `
  "backend\IntegrationTests\IntegrationTests.csproj" `
  --filter "FullyQualifiedName~PostgreSqlLargeFixtureApiBaselineTests" `
  -m:1
```

The existing opt-in test now reports:

- HTTP p50/p95/p99 and payload size;
- average SQL command count per request;
- EF command duration p50/p95/p99;
- non-database request time, calculated as HTTP duration minus captured EF
  command duration;
- representative generated SQL for every distinct command shape;
- the existing PostgreSQL `EXPLAIN (ANALYZE, BUFFERS, VERBOSE)` evidence.

The benchmark refreshes PostgreSQL statistics for the large fixture before
measuring requests, keeping plan comparisons deterministic for the disposable
database.

## 2026-09-25 result

The run used 10,000 tasks, 20 noise projects, 20,000 labels, 2,000 activities,
five warmup requests and 30 measured requests per scenario. All 90 measured
requests returned HTTP 200.

| Scenario | Commands/request | EF p95 | HTTP p95 | Non-database p95 |
|---|---:|---:|---:|---:|
| `projects.list` | 1 | 4.359 ms | 11.467 ms | 6.887 ms |
| `project.tasks.page-1` | 3 | 16.899 ms | 33.599 ms | 17.440 ms |
| `project.dashboard` | 5 | 25.275 ms | 35.870 ms | 9.702 ms |

The task page uses three commands because authorization/access validation,
the paged task query and the count query are separate database operations.
The dashboard uses five commands: access validation, task statistics, overdue
tasks, upcoming tasks and recent activity.

These measurements support two conclusions:

1. There is no N+1 explosion in the measured read-only scenarios.
2. Before adding an index or cache, the next experiment should define whether
   reducing round-trips or changing a specific query shape can improve the target
   HTTP p95.

The first projection experiment was intentionally rejected. Replacing the
`Include`-based read with a direct `ProjectTaskView` projection increased the
10,000-task dashboard p95 from `35.870 ms` to `39.666 ms` and changed the task
page plan execution from `7.907 ms` to `11.974 ms`. The production code was
restored; this result is evidence against keeping that projection without a
different query shape or a new measurement.

## Index candidate follow-up

A separate disposable before/after experiment measured
`(ProjectId, CreatedAt DESC)` for the task page with the same 10,000-task
fixture:

| Phase | HTTP p95 | Representative plan execution |
|---|---:|---:|
| Without candidate index | 22.272 ms | 5.114 ms |
| With candidate index | 19.060 ms | 0.370 ms |

Both phases returned 30/30 successful requests. The candidate was promoted to
the `AddProjectTaskCreatedAtIndex` migration. The production query shape and
dashboard round-trip count were not otherwise changed.

This diagnostic did not approve a migration by itself. A separate disposable
before/after experiment for task pagination did approve the
`(ProjectId, CreatedAt DESC)` index after measuring both HTTP p95 and the
PostgreSQL plan. The migration is documented in
[`V6_QUERY_PLAN_FINDINGS.md`](V6_QUERY_PLAN_FINDINGS.md). This diagnostic still
does not support combining dashboard queries prematurely; that would change
module ownership and transaction/read-consistency behavior and requires a
separate design decision.
