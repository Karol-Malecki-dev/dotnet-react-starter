# V6 Performance and Reliability Release Gate

V6 is accepted only when the repository evidence and the reliability contracts
below are reproducible. This gate is separate from the V5 staging and
production-operations gate in [`V5_RELEASE_GATE.md`](./V5_RELEASE_GATE.md).

## Release scope

V6 release scope is the measured performance and reliability work already
supported by the repository:

- repeatable API and PostgreSQL baseline evidence for representative read paths;
- a measured pagination index change with before/after query-plan evidence;
- notification idempotency for a stable business-event key;
- PostgreSQL outbox claim/lease coordination across worker instances;
- retry state and explicit dead-letter behavior after the retry budget is
  exhausted;
- one aggregate outbox metrics query exposed through `/metrics`;
- frontend single-flight token refresh after concurrent `401` responses;
- cancellation of obsolete frontend task-list reads.

V6 does not require adding infrastructure without a measured need.

## Repository gate

Run these checks from the repository root on the exact commit intended for
release:

```powershell
dotnet build backend\backend.slnx --configuration Release
dotnet test backend\UnitTests\UnitTests.csproj --configuration Release
dotnet test backend\IntegrationTests\IntegrationTests.csproj --configuration Release

Set-Location frontend
npm ci
npm run test:once
npm run build
Set-Location ..
```

The PostgreSQL integration tests and the Docker Compose smoke tests require a
running Docker daemon. The complete local application gate, including the
isolated Compose project and browser smoke tests, is:

```powershell
.\scripts\Invoke-E2ETests.ps1
```

The script uses a unique Compose project and free host ports by default. It
must not stop a development stack belonging to another VS Code window.

Validate the Compose model independently as well:

```powershell
docker compose config --quiet
docker compose --env-file .env.example config --quiet
```

## V6 evidence checks

### Baseline and PostgreSQL plans

The baseline protocol and recorded evidence must remain available in:

- [`V6_BASELINE.md`](./V6_BASELINE.md);
- [`V6_LARGE_FIXTURE_API_BASELINE.md`](./V6_LARGE_FIXTURE_API_BASELINE.md);
- [`V6_QUERY_PLAN_FINDINGS.md`](./V6_QUERY_PLAN_FINDINGS.md);
- [`V6_EF_SQL_TIMING.md`](./V6_EF_SQL_TIMING.md).

To collect a new opt-in large-fixture measurement, run the disposable
Testcontainers scenario described by the baseline document:

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

For a measurement against an already-running benchmark environment, use the
authenticated runner instead:

```powershell
$env:V6_BASELINE_ACCESS_TOKEN = "<short-lived benchmark token>"

pwsh -File .\scripts\Measure-ApiBaseline.ps1 `
  -BaseUrl http://localhost:5000 `
  -ProjectId 00000000-0000-0000-0000-000000000000 `
  -WarmupRequests 5 `
  -MeasuredRequests 50 `
  -OutputPath .\artifacts\v6\baseline-local.md
```

Do not approve a new index, projection, or cache from a benchmark result alone.
Record the query plan, round-trip count, payload, and the before/after
criterion in the evidence document.

### Reliability contracts

The following tests are the release evidence for the V6 reliability slices:

- notification idempotency: `PostgreSqlNotificationIdempotencyTests`;
- outbox lease, retry, and dead-letter behavior:
  `PostgreSqlNotificationEmailOutboxLeasingTests`;
- metrics endpoint and aggregate snapshot:
  `ObservabilityIntegrationTests`;
- frontend refresh coordination:
  `frontend/src/tests/services/api/HttpClient.test.ts`;
- frontend request cancellation:
  `frontend/src/tests/context/ProjectsContext.test.tsx`.

The outbox remains at-least-once at the external email-provider boundary. A
process crash after an external send and before the conditional database
update can still produce a duplicate provider delivery. Provider-level
idempotency or delivery receipts are a separate follow-up, not an unstated V6
release blocker.

## CI and merge gate

The release commit must have a green pull request run for all required CI jobs:

- backend build and tests;
- frontend build and tests;
- production and staging Compose configuration validation;
- Docker Compose smoke tests.

Merge only after:

1. the PR contains the release-gate evidence or links to the committed
   evidence documents;
2. all required checks are successful for the final PR head;
3. no unresolved review comment changes the reliability contract;
4. the branch is merged into `main` without bypassing required checks.

## Explicit exclusions

The following remain intentionally outside the V6 release gate:

- cache or Redis without a measured bottleneck and an invalidation/fallback
  contract;
- a global HTTP idempotency-key middleware;
- provider-level email idempotency or delivery receipts;
- a circuit breaker without an identified external dependency and failure
  budget;
- million-user scalability work without representative workload evidence.

These exclusions keep V6 measurable and prevent a release from being delayed
by speculative infrastructure.

## Final release record

Before tagging V6, record:

- the release commit SHA;
- the merged PR numbers;
- the CI run URL and job conclusions;
- the local or CI Docker smoke result;
- the baseline/query-plan evidence revision;
- the explicit exclusions accepted for follow-up work.
