# Release Readiness Audit

## Scope

This audit records the verification state of the `.NET 9 + React 19` starter after the frontend migration from Create React App to Vite and Vitest, plus the PostgreSQL integration-test and observability work.

The audit covers documentation consistency, build and test evidence, dependency risk, Docker packaging,
VPS deployment operations, recovery automation, and known release limitations. It is not a penetration
test or a certification of production security.

## Verification Matrix

| Area | Verification | Result | Status |
|---|---|---:|---|
| Backend unit tests | `dotnet test backend/UnitTests/UnitTests.csproj` | 333/333 passed | Verified on 2026-09-02 |
| Backend integration tests | `dotnet test backend/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName!~PostgreSqlIntegrationTests"` | 103/103 passed | Verified on 2026-09-02 |
| PostgreSQL Testcontainers tests | `dotnet test ... --filter "FullyQualifiedName~PostgreSqlIntegrationTests"` | 18/18 passed | Verified with `postgres:16-alpine` on 2026-09-02 |
| Complete .NET test gate | `scripts/Invoke-E2ETests.ps1` | 457/457 passed | Verified on 2026-09-02 |
| Frontend tests | `npm run test:once` in `frontend/` | 78/78 passed | Verified on 2026-09-02 |
| Frontend production build | `npm run build` in `frontend/` | Passed | Verified on 2026-09-02 |
| Backend and frontend Docker images | `scripts/Invoke-E2ETests.ps1` | Built successfully | Verified locally on 2026-09-02 |
| Docker Compose E2E/smoke tests | `scripts/Invoke-E2ETests.ps1` | 4/4 Playwright smoke tests passed | Verified locally on 2026-09-02 |

The Docker-enabled local gate completed on 2026-09-02. It built both application images and verified
the complete .NET, frontend, PostgreSQL Testcontainers, Docker Compose, and Playwright smoke suites.
Target-environment acceptance remains a separate staging gate.

## Observability Verification

| Area | Contract | Result | Status |
|---|---|---:|---|
| Liveness | `/health/live` does not depend on the database | HTTP 200 | Verified |
| Readiness | `/health/ready` checks database connectivity | HTTP 200 with PostgreSQL | Verified |
| Worker health | `/health/workers` exposes worker freshness status | HTTP 200 after worker startup | Verified |
| Dependency health | `/health/storage`, `/health/malware-scanner`, and `/health/email` expose dedicated probes | HTTP 200 in integration host | Verified on 2026-09-02 |
| Request correlation | `X-Correlation-ID` is propagated to the response and Serilog context | Verified | Verified |

The worker state is process-local and intentionally does not replace centralized metrics, distributed tracing, or durable job monitoring.

## Toolchain State

- Backend: ASP.NET Core 9 and .NET 9.
- Frontend: React 19, TypeScript, Vite, and Vitest.
- Frontend production output: `frontend/dist`.
- Runtime serving: nginx serves the Vite output from `/usr/share/nginx/html`.
- Public frontend configuration uses `VITE_API_URL`.
- CI command names remain compatible with the existing workflow: `npm run test:once` and `npm run build`.

## Dependency Security

On 2026-09-02, a clean frontend install reported 0 known npm vulnerabilities. The .NET direct and
transitive package audit also reported 0 known vulnerable packages after upgrading Swashbuckle and
Testcontainers and removing the obsolete ASP.NET Core abstractions package. No forced dependency
upgrade was used.

## Release Limitations

This repository is ready for a staging release-candidate validation, but the following points remain
environment-specific or intentionally outside repository-only verification:

- The CD workflow can deploy a full-SHA image to the protected VPS staging environment and run public browser smoke; no real staging run has been recorded in this repository yet.
- Production secrets, database migrations, CORS, cookies, TLS, monitoring, rollback, and hosting configuration still require validation on the target environment.
- Prometheus and Alertmanager configuration, dedicated dependency probes, and dashboard provisioning are present, but real metric ingestion and notification delivery still require a staging run.
- Encrypted backup and destructive restore automation is present, but no off-host copy or restore drill has been recorded yet.
- Local Docker image builds, Docker Compose smoke, and PostgreSQL Testcontainers passed; the CI Trivy image scan must still pass for the exact published SHA images.
- Docker Compose defaults are intended for local development and smoke testing, not as production secrets.

## Release Gate

Before tagging a final release, run and record:

```powershell
Set-Location backend
dotnet test UnitTests/UnitTests.csproj
dotnet test IntegrationTests/IntegrationTests.csproj

# Requires Docker Desktop; verifies real EF Core migrations against PostgreSQL.
dotnet test IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~PostgreSqlIntegrationTests"

Set-Location ../frontend
npm ci
npm run test:once
npm run build

Set-Location ..
./scripts/Invoke-E2ETests.ps1
```

The commands should complete successfully on a clean working tree with the target environment configuration.
The protected staging workflow must additionally run the CD deployment, public browser smoke,
Alertmanager notification check, backup copy, restore drill, and manual rollback described in the
V5 release gate.

## Conclusion

The implementation is suitable for a staging release-candidate cycle. It should not be described as
an accepted V5 production release until hosting, secrets, public workflows, alert delivery, off-host
backup, restore, and rollback evidence are recorded for the selected deployment platform.
