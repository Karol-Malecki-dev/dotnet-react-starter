# Release Readiness Audit

## Scope

This audit records the verification state of the `.NET 9 + React 19` starter after the frontend migration from Create React App to Vite and Vitest.

The audit covers documentation consistency, build and test evidence, dependency risk, Docker packaging, and known release limitations. It is not a penetration test or a certification of production security.

## Verification Matrix

| Area | Verification | Result | Status |
|---|---|---:|---|
| Backend unit tests | `dotnet test backend/UnitTests/UnitTests.csproj` | 55/55 passed | Verified |
| Backend integration tests | `dotnet test backend/IntegrationTests/IntegrationTests.csproj` | 42/42 passed | Verified |
| Frontend tests | `npm run test:once` in `frontend/` | 62/62 passed | Verified after Vite migration |
| Frontend production build | `npm run build` in `frontend/` | Passed | Verified after Vite migration |
| Frontend Docker image | `docker build -f frontend/Dockerfile ... frontend` | Passed | Verified after Vite migration |
| Docker Compose E2E/smoke tests | `scripts/Invoke-E2ETests.ps1` | 100/100 passed | Verified after Vite migration |

All verification rows were confirmed during the release validation on 2026-07-26. The frontend tests, production build, frontend image build, and Docker Compose E2E suite were run after the Vite migration.

## Toolchain State

- Backend: ASP.NET Core 9 and .NET 9.
- Frontend: React 19, TypeScript, Vite, and Vitest.
- Frontend production output: `frontend/dist`.
- Runtime serving: nginx serves the Vite output from `/usr/share/nginx/html`.
- Public frontend configuration uses `VITE_API_URL`.
- CI command names remain compatible with the existing workflow: `npm run test:once` and `npm run build`.

## Dependency Security

The frontend dependency graph was reduced from the original CRA result of 44 reported vulnerabilities to 9 total reported vulnerabilities after the migration.

The production-only audit reports 2 moderate vulnerabilities in `react-router` through `react-router-dom`. npm proposes a breaking upgrade to React Router v7. The upgrade was not forced because it requires an application compatibility review and new regression testing.

The remaining audit findings are documented risk items, not evidence that `npm audit fix --force` should be used. No forced dependency upgrade was applied.

## Release Limitations

This repository is ready for a release candidate validation, but the following points remain environment-specific or intentionally outside this change:

- The CD workflow publishes Docker images to GHCR but does not deploy them to a hosting platform.
- Production secrets, database migrations, CORS, cookies, TLS, monitoring, rollback, and hosting configuration must be reviewed for the target environment.
- Docker Compose defaults are intended for local development and smoke testing, not as production secrets.
- The React Router vulnerabilities require a separate, controlled React Router v7 migration decision.

## Release Gate

Before tagging a final release, run and record:

```powershell
Set-Location backend
dotnet test UnitTests/UnitTests.csproj
dotnet test IntegrationTests/IntegrationTests.csproj

Set-Location ../frontend
npm ci
npm run test:once
npm run build

Set-Location ..
./scripts/Invoke-E2ETests.ps1
```

The commands should complete successfully on a clean working tree with the target environment configuration.

## Conclusion

The codebase is suitable for a release candidate. It should not be described as universally production-ready until hosting, secrets, security configuration, dependency decisions, and operational procedures are validated for the selected deployment platform.
