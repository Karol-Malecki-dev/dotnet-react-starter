# V6 Release Evidence

This record captures the repository evidence for the V6 release-candidate
scope. It complements the reusable checklist in
[`V6_RELEASE_GATE.md`](./V6_RELEASE_GATE.md); it is not a replacement for the
V5 staging, backup, restore, rollback, or production promotion evidence.

## Release candidate commit

| Item | Value |
|---|---|
| `main` commit after the V6 slices | `0caa6154012931c0cbd5f77fd3b607af91bf254a` |
| release-candidate branch state | merged into `main` |
| local Docker daemon | unavailable during this session; CI Docker smoke is the runtime evidence |

## Merged V6 slices

| PR | Slice | Merge commit | Required CI |
|---:|---|---|---|
| [#84](https://github.com/Karol-Malecki-dev/dotnet-react-starter/pull/84) | single-flight frontend token refresh | `c2fb93531394f242c3a4123dac3f957c8385403c` | [CI run](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/runs/36186279384) |
| [#85](https://github.com/Karol-Malecki-dev/dotnet-react-starter/pull/85) | cancellation of stale task-list reads | `4a1c15b67b66ee19fc4f2b27626525a1b6572364` | [CI run](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/runs/36189100014) |
| [#87](https://github.com/Karol-Malecki-dev/dotnet-react-starter/pull/87) | offline status and explicit task retry | `162b8968c7333d50b6ee015e8b5e3858fb3d45ca` | [CI run](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/runs/36190686995) |
| [#86](https://github.com/Karol-Malecki-dev/dotnet-react-starter/pull/86) | V6 release gate and evidence contract | `5685ddd7fe089f343774729926ecdb30737b5c04` | [CI run](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/runs/36190325286) |
| [#88](https://github.com/Karol-Malecki-dev/dotnet-react-starter/pull/88) | bounded SMTP delivery timeout | `0caa6154012931c0cbd5f77fd3b607af91bf254a` | [CI run](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/runs/36190947653) |

Every listed CI run completed successfully for backend, frontend, deployment
configuration, and Docker Compose smoke tests.

## Local validation

The following checks passed while implementing the final slices:

- focused frontend resilience tests: 27/27;
- full frontend suite before the final backend-only slice: 85/85;
- frontend TypeScript check and Vite production build;
- focused SMTP option tests: 11/11;
- backend build: 0 warnings and 0 errors;
- backend unit suite: 334/334;
- local production and staging Compose model validation;
- `git diff --check`.

The local runtime E2E command could not be executed because Docker Desktop did
not expose `dockerDesktopLinuxEngine`. The CI Docker Compose smoke jobs are the
runtime evidence for the exact merged commits and must remain green before
tagging.

## Evidence already recorded

- API baseline protocol and measurements:
  [`V6_BASELINE.md`](./V6_BASELINE.md) and
  [`V6_LARGE_FIXTURE_API_BASELINE.md`](./V6_LARGE_FIXTURE_API_BASELINE.md);
- generated EF SQL, query timings, and PostgreSQL plans:
  [`V6_EF_SQL_TIMING.md`](./V6_EF_SQL_TIMING.md),
  [`V6_QUERY_ANALYSIS.md`](./V6_QUERY_ANALYSIS.md), and
  [`V6_QUERY_PLAN_FINDINGS.md`](./V6_QUERY_PLAN_FINDINGS.md);
- notification idempotency:
  [`V6_IDEMPOTENCY.md`](./V6_IDEMPOTENCY.md);
- outbox lease, retry, dead-letter, and metrics:
  [`V6_OUTBOX_LEASING.md`](./V6_OUTBOX_LEASING.md);
- frontend refresh coordination and request cancellation:
  [`V6_FRONTEND_REQUEST_COORDINATION.md`](./V6_FRONTEND_REQUEST_COORDINATION.md)
  and [`V6_FRONTEND_REQUEST_CANCELLATION.md`](./V6_FRONTEND_REQUEST_CANCELLATION.md).

## Accepted V6 boundaries

V6 intentionally does not add cache/Redis, global HTTP idempotency keys,
provider-level email delivery receipts, or a circuit breaker without a
measured bottleneck and a defined contract. The notification outbox remains
at-least-once at the external provider boundary; a post-send process crash can
still require provider-level deduplication in a future slice.
