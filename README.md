# .NET React Starter

[![CI](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Karol-Malecki-dev/dotnet-react-starter/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=20232A)](https://react.dev/)

Production-minded full-stack starter for applications that need a real
authentication model, a modular backend, a typed React frontend, reliable local
development, and an explicit path to staging operations.

The repository is an executable reference architecture and a learning base. It
is not a ready-made SaaS product and the default local Compose stack is not a
production configuration.

**Polish documentation:** [README.PL.md](README.PL.md)

## What is included

### Identity and account security

- registration, email confirmation, resend-confirmation, password reset, and
  password change flows;
- short-lived JWT access tokens with refresh-token rotation in an HttpOnly
  cookie;
- email-based 2FA and authenticator-app 2FA with recovery codes;
- role-aware authorization for users, administrators, projects, and tasks;
- rate limiting, failed-login lockout, account security events, and neutral
  responses for sensitive account lookups;
- ASP.NET Core Data Protection with a persistent key ring for local Compose and
  deployment environments.

### Collaboration and product slices

- projects with owner/member/viewer roles, invitations, membership changes, and
  project archiving;
- project tasks with pagination, status changes, assignment, labels, due dates,
  comments, activity timeline, and private attachments;
- notifications, unread counts, notification email preferences, and a durable
  email outbox;
- backend-driven runtime configuration and feature flags;
- workspace search, dashboard data, admin views, and user management.

### Reliability and operations

- PostgreSQL persistence through Entity Framework Core and real migrations;
- local filesystem or S3-compatible attachment storage; local Compose uses
  MinIO, while the production topology adds ClamAV scanning;
- background workers for notification email delivery, deadline reminders, and
  attachment cleanup/reconciliation;
- structured Serilog logs, request correlation through `X-Correlation-ID`,
  dedicated health endpoints, and Prometheus-compatible outbox metrics;
- Docker Compose for local development and smoke testing;
- GitHub Actions CI, immutable GHCR image publication, pinned Trivy image
  scanning, protected staging deployment, and a VPS operations runbook.

### Reusable V8 starter workflow

The repository also contains a bounded source template and PowerShell
generators for new consumers and vertical-slice skeletons. The template is
distributed by copy and follows a deliberate `regenerate-and-review` update
policy; it is not a runtime module installer or a general-purpose framework.

See [V8_RELEASE_GATE.md](doc/V8_RELEASE_GATE.md) for the accepted scope and
proof requirements.

## Architecture

The solution is a modular monolith with explicit backend layer boundaries and
feature-oriented frontend slices.

```text
Browser
  |
  v
React + TypeScript + Vite
  |
  v
ASP.NET Core Web API
  |-- PostgreSQL via EF Core
  |-- MinIO or S3-compatible object storage
  |-- SMTP or Mailpit
  `-- Background workers and health checks
```

### Backend boundaries

- `API` owns HTTP binding, controllers, middleware, validation, authorization,
  health endpoints, and the composition root.
- `Application` owns use cases, commands/queries, results, focused ports, and
  application orchestration.
- `Domain` owns entities, value objects, enums, aggregates, and domain
  invariants.
- `Infrastructure` owns EF Core, PostgreSQL migrations, persistence adapters,
  storage, email delivery, workers, and external integrations.
- `Shared` contains deliberately shared settings, DTOs, and response helpers.

New command/query slices use the canonical MediatR `ISender`,
`IRequest<TResult>`, and `IRequestHandler<TRequest, TResult>` path. Existing
areas are migrated incrementally rather than through a repository-wide rewrite.

### Frontend boundaries

- `context/` and `hooks/` own cross-feature session and runtime configuration
  state;
- `services/api/` contains the shared HTTP client and API adapters;
- `features/` contains feature-local API clients, types, state, pages, and
  components;
- `components/` contains the application shell and reusable UI;
- `pages/` contains application-level screens and routes;
- `tests/` contains focused React, API-client, context, and page tests.

The frontend waits for both runtime configuration and authentication bootstrap
before rendering protected UI. The backend remains the source of truth for
authorization and feature availability.

## Technology stack

| Area | Technologies |
| --- | --- |
| Backend | .NET 9, ASP.NET Core Web API, EF Core, Npgsql, PostgreSQL 16, MediatR, FluentValidation, Serilog, Swagger |
| Frontend | React 19, TypeScript, Vite, React Router 7, React Hook Form, Zod, Vitest, Testing Library |
| Runtime | Docker, Docker Compose, Nginx, Caddy |
| Integrations | S3-compatible storage/MinIO, MailKit/SMTP, Mailpit, ClamAV |
| Operations | GitHub Actions, GHCR, Trivy, Prometheus, Alertmanager, Grafana, Blackbox Exporter |
| Testing | xUnit, Moq, Testcontainers for PostgreSQL, Playwright smoke tests |

The repository pins the .NET SDK through
[global.json](global.json). The CI and container builds use Node.js 22.

## Repository structure

```text
.
├── backend/
│   ├── API/                  # HTTP adapters, middleware, startup, auth
│   ├── Application/          # use cases, ports, DTOs, and modules
│   ├── Domain/               # entities, aggregates, value objects, invariants
│   ├── Infrastructure/       # EF Core, migrations, storage, email, workers
│   ├── Shared/               # shared settings, responses, and DTOs
│   ├── UnitTests/            # unit and architecture tests
│   ├── IntegrationTests/     # API, persistence, PostgreSQL, and observability tests
│   ├── E2ETests/             # smoke tests against a running application
│   └── Dockerfile            # deployable API image
├── frontend/
│   ├── src/features/projects/ # feature-oriented project UI
│   ├── src/components/        # application shell and shared UI
│   ├── src/services/          # shared frontend infrastructure
│   └── Dockerfile             # production React/Nginx image
├── deploy/vps/                # staging/production Compose, TLS, monitoring, backups
├── scripts/                   # test orchestration, evidence, and V8 generators
├── templates/v8-consumer/     # canonical reusable source template
├── doc/                       # architecture, setup, security, operations, roadmap
├── docker-compose.yml         # local development and smoke-test stack
└── .github/workflows/         # CI and CD workflows
```

## Quick start

### Prerequisites

- Git;
- .NET 9 SDK;
- Node.js 22 or newer;
- Docker Desktop or Docker Engine with the Compose plugin.

### Recommended: run the complete local stack

From the repository root:

```powershell
git clone https://github.com/Karol-Malecki-dev/dotnet-react-starter.git
Set-Location dotnet-react-starter
Copy-Item .env.example .env
docker compose up --build
```

The local stack starts PostgreSQL, Mailpit, MinIO, the ASP.NET Core API, and the
React/Nginx frontend.

| Component | URL | Notes |
| --- | --- | --- |
| Frontend | `http://localhost:3000` | React application served by Nginx |
| API | `http://localhost:5000` | Compose HTTP endpoint |
| Swagger UI | `http://localhost:5000/swagger` | Development environment only |
| API health | `http://localhost:5000/health` | API and ready-tagged dependencies, excluding workers |
| Mailpit | `http://localhost:8025` | Local email inbox and inspection UI |
| MinIO API | `http://localhost:9000` | Local S3-compatible endpoint |
| MinIO console | `http://localhost:9001` | Local development console |

Transactional emails stay inside Mailpit. Use it to retrieve confirmation
links, password-reset messages, and 2FA codes without sending real email.

Stop the stack without removing data:

```powershell
docker compose down
```

`docker compose down -v` also removes PostgreSQL, MinIO, and Data Protection
volumes. Removing `data-protection-keys` invalidates the key ring used to
protect authenticator secrets, so use the volume-removing command only when a
full local reset is intentional.

### Run the API and frontend locally

The local API launch profile uses port `5031`. First provide a PostgreSQL
connection string and a development JWT secret through User Secrets or
environment variables; the details are described in
[BACKEND_SETUP.md](doc/BACKEND_SETUP.md).

Backend:

```powershell
dotnet restore backend\backend.slnx
dotnet run --project backend\API\API.csproj --launch-profile http
```

Frontend, in a second terminal:

```powershell
Copy-Item frontend\.env.example frontend\.env.development.local
Set-Location frontend
npm ci
npm start
```

For this launch profile, keep the following value in
`frontend\.env.development.local`:

```env
VITE_API_URL=http://localhost:5031
```

The Vite development server is normally available at
`http://localhost:3000`. If you deliberately run the API on another port,
change `VITE_API_URL` and the matching CORS origin together.

## Configuration and security

Configuration has two main entry points:

1. the root `.env` file for Docker Compose and backend runtime values;
2. `frontend\.env.*` files for public, build-time Vite values.

Start from the tracked examples:

```powershell
Copy-Item .env.example .env
Copy-Item frontend\.env.example frontend\.env.development.local
```

Important rules:

- never commit `.env`, credentials, JWT secrets, SMTP passwords, or storage
  keys;
- never put secrets in `VITE_*` variables because they are compiled into public
  frontend assets;
- use `/api` as `VITE_API_URL` when the frontend is served behind the
  production reverse proxy;
- replace every local/example password and secret before using a non-local
  environment;
- keep the Data Protection key ring on persistent storage;
- configure `ForwardedHeaders` with the actual trusted proxy network instead
  of trusting arbitrary addresses;
- use HTTPS, `Always` secure refresh cookies, explicit CORS origins, external
  SMTP, private object storage, and required malware scanning in production.

The local root Compose file is intentionally convenient and uses development
defaults. The production/staging configuration is separate and is documented
in [V5_DEPLOYMENT_RUNBOOK.md](doc/V5_DEPLOYMENT_RUNBOOK.md).

## Runtime endpoints and authentication

### Operational endpoints

| Endpoint | Responsibility |
| --- | --- |
| `GET /health` | Base API health, database, and ready-tagged dependencies; workers are excluded |
| `GET /health/live` | Process liveness without external dependency checks |
| `GET /health/ready` | Readiness for the database, object storage, and malware scanner when configured |
| `GET /health/workers` | Freshness and last-result state of hosted background workers |
| `GET /health/storage` | Attachment object-storage health |
| `GET /health/malware-scanner` | Attachment malware-scanner health |
| `GET /health/email` | Email delivery health |
| `GET /metrics` | Prometheus-compatible notification outbox metrics |
| `GET /api/runtime-config` | Safe backend-driven UI feature configuration |

Every API response carries `X-Correlation-ID`. Clients may provide one to
correlate a request with structured logs; the API generates one when it is
missing.

### Authentication flow

1. `POST /api/auth/register` creates an account and starts email confirmation.
2. `POST /api/auth/confirm-email` activates the account.
3. `POST /api/auth/login` either completes sign-in or starts an email or
   authenticator-app 2FA challenge.
4. `POST /api/auth/verify-2fa` completes a pending challenge and issues the
   access token plus refresh-token cookie.
5. `POST /api/auth/refresh-token` rotates the refresh token and returns a new
   access token.
6. `POST /api/auth/logout` or `POST /api/auth/logout-all` revokes sessions.
7. `POST /api/auth/forgot-password` and `POST /api/auth/reset-password` handle
   password recovery.
8. `GET /api/auth/me` returns the authenticated user profile.

The frontend stores the access token and a user snapshot locally. The refresh
token is kept only in an HttpOnly cookie and is not exposed to JavaScript.
Authenticator secrets are protected by ASP.NET Core Data Protection.

## Testing and quality gates

Run the smallest relevant check first, then use the full gate before merging.

### Backend

```powershell
dotnet restore backend\backend.slnx
dotnet build backend\backend.slnx --configuration Release
dotnet test backend\UnitTests\UnitTests.csproj --configuration Release
dotnet test backend\IntegrationTests\IntegrationTests.csproj --configuration Release
```

Most integration tests use a controlled in-memory setup. The PostgreSQL
integration suite starts a real `postgres:16-alpine` Testcontainers instance,
applies the real EF Core migrations, and therefore requires a working Docker
Engine:

```powershell
docker info
dotnet test backend\IntegrationTests\IntegrationTests.csproj `
  --filter "FullyQualifiedName~PostgreSqlIntegrationTests"
```

`backend\E2ETests` contains smoke tests against an already running stack. Use
the full local smoke gate below, or set `SMOKE_API_URL` and
`SMOKE_FRONTEND_URL` before running that project directly.

### Frontend

```powershell
Set-Location frontend
npm ci
npm run test:once
npm run build
```

### Full local smoke gate

The orchestration script builds a temporary Compose stack, runs the backend and
frontend checks, executes browser smoke tests, and cleans up its own stack:

```powershell
.\scripts\Invoke-E2ETests.ps1
```

The `E2ETests` and Playwright suites are deployment smoke tests. They validate
critical paths and service availability; they are not a claim of complete
business-flow coverage.

## CI/CD and deployment

### CI

[`ci.yml`](.github/workflows/ci.yml) runs for pull requests, pushes to `main`
and `feature/**`, and manual dispatches. It validates:

- backend restore, Release build, unit tests, and integration tests;
- frontend dependency installation, Vitest tests, and production build;
- production and staging Compose models, shell scripts, PowerShell syntax,
  Caddy, Prometheus, and Alertmanager configuration;
- Docker Compose application images and browser smoke tests.

### CD

[`cd.yml`](.github/workflows/cd.yml) runs after successful CI on `main` or by
manual dispatch. It:

1. builds backend and frontend images;
2. scans them with a pinned Trivy image for HIGH and CRITICAL vulnerabilities;
3. publishes immutable full-commit-SHA tags to GHCR;
4. can deploy the exact SHA to a protected VPS staging environment;
5. runs public HTTPS browser smoke tests and captures release evidence.

Publishing an image is not the same as deploying production. Production
promotion remains an explicit operator decision and requires the release gates,
backup/restore evidence, alert delivery validation, and rollback target
described in [CI_CD.md](doc/CI_CD.md) and
[V5_DEPLOYMENT_RUNBOOK.md](doc/V5_DEPLOYMENT_RUNBOOK.md).

### Production topology

The separate `deploy\vps` path targets one hardened Linux VPS running Docker
Compose:

- Caddy terminates public TLS; the frontend and API remain private;
- a one-shot migration container runs before the API;
- PostgreSQL and MinIO use persistent volumes;
- ClamAV scans attachments before they become available;
- Prometheus, Alertmanager, Blackbox Exporter, Node Exporter, and Grafana
  provide health, alerting, and host observability;
- encrypted backups, restore drills, and manual rollback are part of the
  operational contract.

Do not use the local root Compose file or its example credentials as a
production deployment procedure.

## V8 scaffolding

Generate a bounded consumer project:

```powershell
.\scripts\New-V8StarterProject.ps1 `
  -Destination .\artifacts\v8\consumer `
  -Variant Minimal
```

Add a query or command skeleton to a generated consumer:

```powershell
.\scripts\New-VsaSlice.ps1 `
  -RootPath .\artifacts\v8\consumer `
  -Module Catalog `
  -UseCase ListItems `
  -Kind Query
```

Run the reproducible proof for both template variants:

```powershell
.\scripts\Invoke-V8ScaffoldingProof.ps1 `
  -OutputDirectory .\artifacts\v8\release-proof
```

Generated handlers are intentionally marked as skeletons and throw until the
consumer implements its domain rule, authorization, ports, persistence,
transactions, failure mapping, and tests. Read
[templates/v8-consumer/Common/README.md](templates/v8-consumer/Common/README.md)
before treating generated files as application code.

## Documentation map

| Start here when you need to... | Read |
| --- | --- |
| Run the project for the first time | [GETTING_STARTED.md](doc/GETTING_STARTED.md) |
| Understand module and layer boundaries | [ARCHITECTURE.md](doc/ARCHITECTURE.md) |
| Configure the backend and persistence | [BACKEND_SETUP.md](doc/BACKEND_SETUP.md) |
| Understand the frontend bootstrap and API client | [FRONTEND_SETUP.md](doc/FRONTEND_SETUP.md) |
| Understand JWT, sessions, and refresh rotation | [JWT_ARCHITECTURE.md](doc/JWT_ARCHITECTURE.md) |
| Understand email confirmation, 2FA, and password reset | [EMAIL_2FA_FLOWS.md](doc/EMAIL_2FA_FLOWS.md) |
| Add a backend or frontend capability | [ADDING_FEATURES.md](doc/ADDING_FEATURES.md) |
| Understand Docker Compose locally | [DOCKER_COMPOSE.md](docker/DOCKER_COMPOSE.md) |
| Understand CI, CD, and staging | [CI_CD.md](doc/CI_CD.md) |
| Operate the VPS deployment | [V5_DEPLOYMENT_RUNBOOK.md](doc/V5_DEPLOYMENT_RUNBOOK.md) |
| Configure environment variables and secrets | [CONFIGURATION_REFERENCE.md](doc/CONFIGURATION_REFERENCE.md) |
| Troubleshoot local, test, and deployment failures | [TROUBLESHOOTING.md](doc/TROUBLESHOOTING.md) |
| Integrate with the HTTP API | [API_REFERENCE.md](doc/API_REFERENCE.md) |
| Contribute code, documentation, or templates | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Report a security vulnerability | [SECURITY.md](SECURITY.md) |
| Review repository release history | [CHANGELOG.md](CHANGELOG.md) |
| Review performance and reliability gates | [V6_RELEASE_GATE.md](doc/V6_RELEASE_GATE.md) |
| Review the reusable starter release | [V8_RELEASE_GATE.md](doc/V8_RELEASE_GATE.md) |
| Follow the broader product roadmap | [ROADMAP/00_ROADMAP_OVERVIEW.md](doc/ROADMAP/00_ROADMAP_OVERVIEW.md) |
| Review recorded release evidence | [RELEASE_READINESS_AUDIT.md](doc/Audits/RELEASE_READINESS_AUDIT.md) |

For repository-wide engineering rules, see
[.github/copilot-instructions.md](.github/copilot-instructions.md) and the
backend-specific [backend.instructions.md](.github/instructions/backend.instructions.md).

## Scope and limitations

- The default Compose stack is for local development and testing. Its
  credentials, HTTP configuration, and exposed service ports are examples.
- The deployment path is intentionally a controlled single-VPS model, not an
  automatic multi-region platform or Kubernetes distribution.
- The V8 template is a source-copy baseline. Consumers own their generated
  source and update it through review.
- Smoke tests cover critical availability and user journeys, not every possible
  business rule or production traffic profile.

## License

MIT. See [LICENSE](LICENSE).
