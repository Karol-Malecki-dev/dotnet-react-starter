# V6 PostgreSQL Query Analysis

This slice adds a repeatable diagnostic for the PostgreSQL paths behind the API
baseline. It captures query plans before changing indexes, projections, tracking,
pagination, or cache behavior.

## Scope

The diagnostic mirrors the current read paths:

- visible project list;
- project task count used by paged task listing;
- project task page with labels and ordering;
- dashboard task statistics;
- dashboard overdue and upcoming task lists;
- recent project activity.

The source implementations are:

- `Infrastructure/Modules/ProjectTasks/ListProjectTasks/EfProjectTaskQueryStore.cs`;
- `Infrastructure/Modules/ProjectTasks/Dashboard/EfProjectTaskDashboardReader.cs`;
- `Infrastructure/Modules/Projects/ListProjects/EfListProjectsStore.cs`;
- `Infrastructure/Modules/Projects/GetProjectDashboard/EfGetProjectDashboardStore.cs`.

The SQL is intentionally kept in a diagnostic script rather than added to the
application runtime. It must be kept aligned with the store implementations when
the read contract changes.

For a disposable PostgreSQL-backed fixture, the integration test
`PostgreSqlQueryPlanEvidenceTests.PostgreSql_query_plan_evidence_is_captured_for_v6_fixture`
seeds 20 noise projects, 1,000 benchmark tasks, labels, and activity, then writes
the same kind of evidence to `artifacts/v6/`. This is the reproducible local
evidence path used by [`V6_QUERY_PLAN_FINDINGS.md`](./V6_QUERY_PLAN_FINDINGS.md).

## Running the diagnostic

Run it against a disposable local database or a staging clone with the same
fixture used by [`V6_BASELINE.md`](./V6_BASELINE.md):

```powershell
pwsh -File .\scripts\Capture-PostgresQueryPlans.ps1 `
  -ProjectId 00000000-0000-0000-0000-000000000000 `
  -UserId 00000000-0000-0000-0000-000000000000 `
  -ComposeFile .\docker-compose.yml `
  -DbService db `
  -DbUser postgres `
  -DbName dotnetreact `
  -ApplicationVersion $env:GITHUB_SHA `
  -FixtureDescription '20 visible projects; benchmark project with 1000 tasks' `
  -OutputPath .\artifacts\v6\query-plans-local.md
```

For a deployment Compose model, pass its environment file explicitly. The
environment file is consumed by Docker Compose and is never written to the report:

```powershell
pwsh -File .\scripts\Capture-PostgresQueryPlans.ps1 `
  -ProjectId <benchmark-project-id> `
  -ComposeFile .\deploy\vps\compose.production.yml `
  -EnvFile <path-to-runtime-env-file> `
  -DbService db `
  -DbUser <runtime-db-user> `
  -DbName <runtime-db-name> `
  -ApplicationVersion <immutable-image-tag> `
  -FixtureDescription '<fixture summary>'
```

The report is written below `artifacts/v6/`, which is ignored by Git. Do not commit
database credentials, access tokens, or real user data.

The report includes PostgreSQL/database metadata, global and selected-project row
counts, the application version, fixture description, query parameters, and the
plan output. Pass `-UserId` to include the visible-project query; without it, that
query is explicitly skipped. When `-ApplicationVersion` is omitted, the script uses
the current Git commit when available; otherwise it records `not provided`.

## Safety and interpretation

The script executes only `SELECT` statements wrapped in
`EXPLAIN (ANALYZE, BUFFERS, VERBOSE)`. `ANALYZE` still executes the query and consumes
database resources, so do not run it against production during peak traffic.

Review at least:

- planning and execution time;
- sequential scans versus natural index scans;
- rows removed by filters;
- shared and local buffer hits/reads;
- sort method and spill behavior;
- whether the plan matches the API latency report.

The existing PostgreSQL integration test may disable sequential scans only to prove
that a specific dashboard index is usable. That is not a performance baseline. The
diagnostic runner must use the database's natural planner decisions.

Do not add an index, Redis, or a projection rewrite merely because a plan looks
complex. First record the bottleneck, expected improvement, fixture, and measurable
success threshold. Then create a separate implementation slice and compare before
and after reports.
