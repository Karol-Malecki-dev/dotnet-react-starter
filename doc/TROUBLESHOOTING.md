# Troubleshooting

Use this page after checking the relevant setup document:

- [Getting started](GETTING_STARTED.md) for local prerequisites;
- [Configuration reference](CONFIGURATION_REFERENCE.md) for variables;
- [Backend setup](BACKEND_SETUP.md) for persistence and startup;
- [CI/CD](CI_CD.md) for pipeline failures;
- [VPS deployment runbook](V5_DEPLOYMENT_RUNBOOK.md) for operations.

Do not paste secrets, JWTs, cookies, reset links, recovery codes, SMTP
credentials, or personal data into issues or logs shared outside the
authorized team.

## A repeatable diagnostic order

1. Confirm the command is running from the repository root.
2. Confirm Docker Desktop/Docker Engine is running when using Compose or
   Testcontainers.
3. Check the interpolated Compose model:

   ```powershell
   docker compose config
   ```

4. Check service status and recent logs:

   ```powershell
   docker compose ps
   docker compose logs --tail=200 backend
   docker compose logs --tail=200 db
   ```

5. Probe the API and its dependency-specific health checks:

   ```powershell
   Invoke-WebRequest http://localhost:5000/health
   Invoke-WebRequest http://localhost:5000/health/live
   Invoke-WebRequest http://localhost:5000/health/ready
   Invoke-WebRequest http://localhost:5000/health/workers
   Invoke-WebRequest http://localhost:5000/health/storage
   Invoke-WebRequest http://localhost:5000/health/email
   ```

6. Record the `X-Correlation-ID` from a failing response and search the
   structured backend logs for that request.
7. Re-run the smallest relevant test or request after changing one variable.

## Local startup problems

### Docker Compose does not start

Check:

```powershell
docker version
docker compose version
docker compose config
docker compose ps
```

If another stack owns a host port, change `POSTGRES_PORT`, `MAILPIT_*_PORT`,
`MINIO_*_PORT`, `BACKEND_HTTP_PORT`, or `FRONTEND_HTTP_PORT` in `.env`. Keep
the internal service names (`db`, `mailpit`, `minio`, and `backend`) unchanged.
`COMPOSE_PROJECT_NAME` can isolate volumes and network names when multiple
stacks share a machine.

### The API container exits during startup

Inspect the full startup error:

```powershell
docker compose logs backend
docker compose logs db
docker compose logs minio-init
```

Common causes are:

- PostgreSQL is not healthy or the connection string points to `localhost`
  from inside the container;
- a required configuration value is malformed;
- a migration fails because the database schema or credentials are wrong;
- `minio-init` cannot authenticate or create the attachment bucket;
- a persistent Data Protection volume has incorrect permissions.

Do not solve a migration or credential error by deleting volumes blindly.
Identify the failing dependency first. `docker compose down -v` removes local
database, MinIO, and Data Protection data and should only be used when that data
is disposable.

### The frontend loads but API calls fail

Confirm the browser-visible API base URL:

- Compose frontend: `VITE_API_URL`/`FRONTEND_REACT_APP_API_URL` should normally
  be `/api`;
- standalone Vite: use `VITE_API_URL=http://localhost:5031`;
- standalone API profile: the HTTP port is `5031`;
- Compose API: the host URL is `5000`.

Vite values are embedded at build time. Rebuild the frontend after changing
them:

```powershell
docker compose build frontend
docker compose up -d frontend
```

### Port `5000` or `5031` is confusing

`5000` is the host port published by the root Compose stack. `5031` is the
HTTP port in `backend/API/Properties/launchSettings.json` for a backend run
outside Compose. They are alternative local modes, not two required API
instances.

## Database and migrations

### PostgreSQL is unhealthy

Check credentials and the actual published port:

```powershell
docker compose ps db
docker compose logs --tail=200 db
```

The health check uses `POSTGRES_USER` and `POSTGRES_DB`. If those values were
changed after the volume was initialized, PostgreSQL may still contain the
original cluster credentials. For disposable local data, recreate the volume
only after confirming it is safe to lose.

### A migration fails

Capture the backend/migration error and confirm that:

- the target database is reachable;
- the application image contains the intended migrations;
- no second migration process is running concurrently;
- the database user can apply the required schema changes.

Local Compose may apply migrations at startup. Production uses the one-shot
migration container and keeps `Database__ApplyMigrationsOnStartup=false` for
the long-running backend. Follow the deployment runbook for rollback and
forward-fix decisions; do not edit the database manually as a first response.

## Authentication, cookies, and CORS

### Login succeeds but the browser has no session

Inspect the response and browser cookie storage without copying the cookie
value. Verify:

- the request uses the same public origin as the configured CORS origin;
- HTTPS is used when the cookie policy requires `Secure`;
- the refresh cookie path includes `/api/auth`;
- the browser request sends credentials;
- the API has a persistent Data Protection key ring;
- a reverse proxy forwards the scheme and the backend trusts only the known
  proxy network.

The access token is returned to the client after successful authentication;
the refresh token is written to an HttpOnly cookie. Do not move the refresh
token into local storage to work around a cookie problem.

### CORS rejects a request

Compare the browser `Origin` header with `Cors:AllowedOrigins` and ensure the
origin includes the exact scheme and port. Do not use a wildcard when
credentials are enabled. The configured Vite default is
`http://localhost:3000`, while `http://localhost:5173` is supported only when
the development server is explicitly started on that alternate port.

### Refresh or logout suddenly fails after a restart

Check whether the JWT/Data Protection configuration or persistent volumes were
changed. Refresh tokens are rotated and revoked; a revoked token is expected
to fail. Deleting the Data Protection key ring also invalidates protected
cookies. The correct recovery is to authenticate again and investigate the
change, not to disable rotation.

### Email confirmation or 2FA does not arrive

For local development:

1. verify `EMAIL_DELIVERY_ENABLED=true`;
2. verify the API can resolve `mailpit:1025`;
3. open `http://localhost:8025`;
4. inspect `docker compose logs backend`.

For staging/production, verify SMTP host, port, STARTTLS, sender address,
credentials, provider allow-lists, and timeout. The API intentionally returns
neutral responses for several account-sensitive flows; an HTTP 200 does not
prove that a message was delivered to a real mailbox.

## Storage and attachments

### Attachment health is degraded

Check:

```powershell
docker compose logs --tail=200 minio minio-init backend
Invoke-WebRequest http://localhost:5000/health/storage
```

Verify bucket name, endpoint, path-style setting, application credentials, and
that `minio-init` completed. Keep the bucket private and never expose the
MinIO root credentials to the frontend.

### Production attachments are rejected

Production requires malware scanning. Check `/health/malware-scanner`, ClamAV
health, scan timeout, and the backend logs. A rejected or timed-out scan is a
security failure path; do not disable scanning merely to accept a file.

Review [ATTACHMENT_OPERATIONS.md](ATTACHMENT_OPERATIONS.md) for retention,
reconciliation, backup, and restore behavior.

## Tests and builds

### Integration tests cannot start PostgreSQL

Testcontainers requires a working Docker Engine and permission to pull/start
the PostgreSQL image. Check:

```powershell
docker version
dotnet test backend/IntegrationTests/IntegrationTests.csproj --configuration Release
```

If Docker is unavailable, run unit tests separately and report integration
tests as blocked rather than changing the tests to bypass PostgreSQL.

### E2E tests fail immediately

The E2E project targets a running API and frontend; it does not start the full
application by itself. Start Compose first or use the orchestration script:

```powershell
.\scripts\Invoke-E2ETests.ps1
```

Override endpoints when testing another environment:

```powershell
$env:SMOKE_API_URL = "http://localhost:5000"
$env:SMOKE_FRONTEND_URL = "http://localhost:3000"
dotnet test backend/E2ETests/E2ETests.csproj
```

If the browser cannot reach the API, verify the frontend build-time API base
URL and the public proxy route before changing Playwright timeouts.

### Frontend dependencies or build fail

Use the lockfile and a clean install:

```powershell
Set-Location frontend
npm ci
npm run test:once
npm run build
Set-Location ..
```

Node.js 22 is the CI/container baseline. Avoid upgrading packages as a
diagnostic shortcut; first capture the actual TypeScript, Vite, or test error.

## Production and staging

### The migration job blocks the backend

Inspect the one-shot job:

```bash
docker compose -f compose.production.yml --env-file .env.production \
  ps migration
docker compose -f compose.production.yml --env-file .env.production \
  logs migration
```

The backend is intentionally dependent on a successful migration job. Verify
the image SHA, database health, connection settings, and migration output
before rerunning it.

### Caddy does not issue a certificate

Verify DNS, inbound ports `80`/`443`, `APP_DOMAIN`, `TLS_EMAIL`, and Caddy
logs. Do not expose backend, PostgreSQL, MinIO, or monitoring ports to the
Internet as a workaround. The public route should be the only externally
reachable application boundary.

### Readiness fails after deployment

Check these endpoints and dependencies in order:

1. `/health/live`;
2. `/health/ready`;
3. `/health/storage`;
4. `/health/malware-scanner`;
5. `/health/email`;
6. `/health/workers`.

Use the deployment evidence and monitoring dashboards to distinguish a
dependency outage, a migration problem, a worker backlog, and a configuration
error. Follow the [VPS deployment runbook](V5_DEPLOYMENT_RUNBOOK.md) for
rollback rather than changing production containers ad hoc.
