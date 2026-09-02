# .NET React Starter

Production-minded full-stack learning starter with ASP.NET Core 9 API, React + TypeScript frontend, JWT authentication, refresh-token rotation in HttpOnly cookies, Docker Compose, and automated test projects.

This repository is a practical base for auth-heavy applications, admin dashboards, and future starter implementations. It already includes backend auth flows, protected frontend routes, role-aware access, Docker wiring, and test projects you can extend.

## Features

- Clean Architecture backend split into `API`, `Application`, `Domain`, `Infrastructure`, and `Shared`
- React + TypeScript frontend with protected routes, authenticated session handling, runtime feature gating, and a quick search shell
- JWT access tokens with secure refresh-token rotation in HttpOnly cookies
- Email confirmation and email-based 2FA during sign-in
- Role-aware authorization for admin-only endpoints and views
- React Hook Form + Zod for frontend form validation
- Serilog logging and centralized exception handling
- Dockerfiles for backend and frontend
- Docker Compose setup with PostgreSQL, Mailpit for local email delivery, backend healthcheck, and frontend proxy-ready API routing
- Unit, integration, and smoke/E2E test projects
- Swagger UI in development

## Current Stack

### Backend

- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL in Docker/runtime scenarios
- FluentValidation
- Serilog
- xUnit + Moq

### Frontend

- React 19
- TypeScript
- React Router
- React Hook Form
- Zod
- Testing Library + Vitest
- Vite build pipeline with Vitest-based frontend tests

## Project Structure

```text
.
├── backend/
│   ├── API/                  # Controllers, middleware, startup, auth configuration
│   ├── Application/          # DTOs, services, validators, interfaces
│   ├── Domain/               # Entities, enums, domain interfaces
│   ├── Infrastructure/       # DbContext, repositories, infrastructure services
│   ├── Shared/               # Shared responses, settings, helpers
│   ├── UnitTests/            # Focused unit tests
│   ├── IntegrationTests/     # Backend integration tests
│   └── E2ETests/             # Deployment smoke tests against a running app
├── frontend/
│   ├── public/
│   └── src/
│       ├── components/
│       ├── context/
│       ├── hooks/
│       ├── pages/
│       ├── services/
│       ├── tests/
│       ├── types/
│       └── utils/
├── docker/
└── doc/
```

## Environment Configuration

This repository uses two configuration entry points:

1. Root `.env` for Docker Compose and backend runtime values.
2. `frontend/.env.*` files for local frontend build-time values.

Start from the tracked examples:

```powershell
Copy-Item .env.example .env
Copy-Item frontend/.env.example frontend/.env.development.local
```

Rules:

- Backend secrets belong in root `.env`, CI/CD secrets, or hosting configuration.
- Frontend `VITE_*` values are public at build time. Never store secrets there.
- For Docker/nginx deployments, use `FRONTEND_REACT_APP_API_URL=/api` (mapped to `VITE_API_URL` during the image build).
- For local frontend to local backend development, use `VITE_API_URL=http://localhost:5000`.
- The Compose example is intentionally `Development` over HTTP; it uses `SameAsRequest` for the refresh cookie.
- Production requires a non-example `JWT_SECRET`, `Always` secure refresh cookies, a persistent Data Protection key ring, and explicitly trusted forwarded proxy networks.

## Runtime Feature Flags

The frontend consumes `GET /api/runtime-config` during startup.

Current flags include:

- `GlobalSearchEnabled`
- `DashboardOverviewEnabled`
- `AdminNavigationEnabled`
- `UserManagementNavigationEnabled`
- `EmailFeatureSectionsEnabled`
- `EmailDeliveryEnabled`
- `EmailTwoFactorEnabled`
- `EmailTwoFactorEnabledForNewUsers`

Use `RuntimeConfigProvider` and `useFeatureAvailability()` to read them from the frontend.

## Quick Start

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- Docker Desktop with Compose support for containerized runs

### Run with Docker

```powershell
git clone https://github.com/Karol8284/dotnet-react-starter.git
Set-Location dotnet-react-starter
Copy-Item .env.example .env
docker compose up --build
```

Default local endpoints:

- Frontend: http://localhost:3000
- API: http://localhost:5000
- Health: http://localhost:5000/health
- Swagger UI: http://localhost:5000/swagger
- Mail UI: http://localhost:8025

### Run Locally Without Docker

Backend:

```powershell
Set-Location backend/API
dotnet run
```

Frontend:

```powershell
Set-Location frontend
npm install
npm start
```

If you run the frontend locally against the local backend, make sure `frontend/.env.development.local` contains:

```text
VITE_API_URL=http://localhost:5000
```

The frontend shell now waits for both auth and runtime config before rendering protected UI. When `GlobalSearchEnabled` is on, `Ctrl+K` opens the quick search bar in the navbar.

The default Docker Compose configuration is intended for local development and smoke testing. The CD
workflow publishes immutable SHA-tagged images and can deploy them to the protected VPS staging
environment, where it runs public HTTPS browser smoke with the staging Mailpit profile. Production
promotion remains manual and requires the V5 release gate, including backup, restore and rollback
evidence.

The backend persists Data Protection keys in the `data-protection-keys` Compose volume.
Keep that volume when restarting the stack; removing it invalidates the key ring used to
protect authenticator secrets.

## Operations

The API exposes health endpoints for deployment probes and monitoring:

- `GET /health` reports the API and database health, excluding background workers.
- `GET /health/live` reports that the API process is alive.
- `GET /health/ready` verifies that the configured database accepts connections.
- `GET /health/workers` reports the latest processing state of background workers.
- `GET /health/storage`, `/health/malware-scanner`, and `/health/email` expose dedicated dependency
	and delivery health for monitoring.

Every response includes `X-Correlation-ID`. Clients can provide this header to trace a request through structured API logs; otherwise the API generates a request identifier.

## Testing

### Backend

```powershell
dotnet test backend/UnitTests/UnitTests.csproj
dotnet test backend/IntegrationTests/IntegrationTests.csproj
dotnet test backend/E2ETests/E2ETests.csproj
```

Test `PostgreSqlIntegrationTests` uruchamia PostgreSQL przez Testcontainers,
dlatego przed wykonaniem pełnego zestawu `IntegrationTests` Docker Desktop musi
działać i udostępniać poprawnie skonfigurowany endpoint Docker Engine. Pozostałe
testy integracyjne korzystają z kontrolowanego store'a in-memory.

The integration suite includes PostgreSQL Testcontainers coverage. It applies the real EF Core migrations to a temporary PostgreSQL container, so Docker Desktop must be running before executing it.

```powershell
docker info
dotnet test backend/IntegrationTests/IntegrationTests.csproj --filter "FullyQualifiedName~PostgreSqlIntegrationTests"
```

Smoke tests in `E2ETests` are meant for a running application, for example after `docker compose up` or after deployment.

To build the containers, wait for the backend and frontend, run the full test solution, and clean up automatically:

```powershell
./scripts/Invoke-E2ETests.ps1
```

Optional smoke-test overrides:

```powershell
$env:SMOKE_API_URL="http://localhost:5000"
$env:SMOKE_FRONTEND_URL="http://localhost:3000"
dotnet test backend/E2ETests/E2ETests.csproj
```

### Frontend

```powershell
Set-Location frontend
npm install
npm run test:once
npm run build
```

## Authentication Overview

- `POST /api/auth/register` creates a user and sends an email confirmation link
- `POST /api/auth/confirm-email` confirms the address and activates the account
- `POST /api/auth/login` returns either JWT tokens or a 2FA email challenge when email 2FA is enabled
- `POST /api/auth/verify-2fa` verifies the email code and then returns the access token plus refresh-token cookie
- `POST /api/auth/resend-2fa` rotates the active sign-in code and sends a fresh email
- `POST /api/auth/refresh-token` rotates the refresh-token cookie and returns a fresh access token
- `POST /api/auth/logout` revokes the refresh token and clears the cookie
- `GET /api/auth/me` returns the authenticated user profile

The frontend stores only the access token client-side. The refresh token stays in an HttpOnly cookie and is not exposed to JavaScript.

For local Docker runs, transactional emails are delivered to Mailpit. Open http://localhost:8025 to see confirmation links and 2FA codes.

## Documentation

- [doc/GETTING_STARTED.md](doc/GETTING_STARTED.md)
- [doc/ARCHITECTURE.md](doc/ARCHITECTURE.md)
- [doc/BACKEND_SETUP.md](doc/BACKEND_SETUP.md)
- [doc/FRONTEND_SETUP.md](doc/FRONTEND_SETUP.md)
- [doc/JWT_ARCHITECTURE.md](doc/JWT_ARCHITECTURE.md)
- [doc/EMAIL_2FA_FLOWS.md](doc/EMAIL_2FA_FLOWS.md)
- [doc/CI_CD.md](doc/CI_CD.md)
- [docker/DOCKER_COMPOSE.md](docker/DOCKER_COMPOSE.md)
- [doc/ROADMAP/00_ROADMAP_OVERVIEW.md](doc/ROADMAP/00_ROADMAP_OVERVIEW.md)
- [backend/DEVELOPMENT_ROADMAP.md](backend/DEVELOPMENT_ROADMAP.md)

## Suggested Next Steps

- Execute V2: stabilization and security hardening
- Add the focused tests and ADRs required by the roadmap before starting new infrastructure work
- Choose a hosting target after the auth and data consistency foundations are stable

## License

MIT. See [LICENSE](LICENSE).