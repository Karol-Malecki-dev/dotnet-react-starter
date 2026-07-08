# .NET React Starter

Production-oriented full-stack starter with ASP.NET Core 9 API, React + TypeScript frontend, JWT authentication, refresh-token rotation in HttpOnly cookies, Docker Compose, and automated test projects.

This repository is a practical base for auth-heavy applications, admin dashboards, and future starter implementations. It already includes backend auth flows, protected frontend routes, role-aware access, Docker wiring, and test projects you can extend.

## Features

- Clean Architecture backend split into `API`, `Application`, `Domain`, `Infrastructure`, and `Shared`
- React + TypeScript frontend with protected routes and authenticated session handling
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
- Testing Library + Jest
- CRA build pipeline via `react-scripts`

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
- Frontend `REACT_APP_*` values are public at build time. Never store secrets there.
- For Docker/nginx deployments, use `FRONTEND_REACT_APP_API_URL=/api`.
- For local frontend to local backend development, use `REACT_APP_API_URL=http://localhost:5000`.

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
REACT_APP_API_URL=http://localhost:5000
```

## Testing

### Backend

```powershell
dotnet test backend/UnitTests/UnitTests.csproj
dotnet test backend/IntegrationTests/IntegrationTests.csproj
dotnet test backend/E2ETests/E2ETests.csproj
```

Smoke tests in `E2ETests` are meant for a running application, for example after `docker compose up` or after deployment.

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
- [docker/DOCKER_COMPOSE.md](docker/DOCKER_COMPOSE.md)

## Suggested Next Steps

- Add full profile editing and change-password flows
- Add CI with GitHub Actions for backend tests, frontend tests, and builds
- Expand admin user management and filtering
- Migrate the frontend from CRA to Vite when the current feature set stabilizes

## License

MIT. See [LICENSE](LICENSE).