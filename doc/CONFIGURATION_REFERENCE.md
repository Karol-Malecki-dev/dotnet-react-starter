# Configuration Reference

This page describes the configuration surface used by the local Compose stack,
the standalone backend/frontend processes, and the VPS deployment. The
repository uses standard ASP.NET Core configuration binding:

```text
Jwt:Secret                 -> Jwt__Secret
Attachments:S3BucketName   -> Attachments__S3BucketName
```

Environment variables override `appsettings.json` and
`appsettings.{Environment}.json`. Docker Compose first interpolates the root
`.env` file and then passes the resulting values to containers. Frontend
`VITE_*` values are build-time values, not runtime secrets.

## Configuration sources

| Use case | Source of truth | Notes |
| --- | --- | --- |
| Local Compose | [.env.example](../.env.example) and [docker-compose.yml](../docker-compose.yml) | Copy to `.env`; never commit the copy. |
| Standalone API | `backend/API/appsettings*.json`, environment variables, User Secrets | The launch profile uses port `5031`. |
| Standalone Vite | `frontend/.env*` and `VITE_*` variables | Values are embedded into the browser bundle. |
| Staging/production VPS | [`.env.production.example`](../deploy/vps/.env.production.example), `compose.production.yml`, `compose.staging.yml` | Production requires explicit secret values and full-SHA image tags. |

When a variable is marked **secret**, use a secret manager or protected host
environment in real deployments. The local values are only development
defaults.

## Local Compose variables

### Ports and infrastructure

| Variable | Default/example | Purpose |
| --- | --- | --- |
| `COMPOSE_PROJECT_NAME` | unset | Optional Compose project name for isolating multiple local stacks. |
| `POSTGRES_DB` | `dotnetreact` | PostgreSQL database name. |
| `POSTGRES_USER` | `postgres` | Local PostgreSQL user. |
| `POSTGRES_PASSWORD` | local example | Local PostgreSQL password (**secret**). |
| `POSTGRES_PORT` | `5432` | Host port mapped to PostgreSQL. |
| `MAILPIT_SMTP_PORT` | `1025` | Host port mapped to Mailpit SMTP. |
| `MAILPIT_HTTP_PORT` | `8025` | Host port mapped to the Mailpit web UI. |
| `MINIO_ROOT_USER` | `minioadmin` | Local MinIO root user (**secret-like credential**). |
| `MINIO_ROOT_PASSWORD` | `minioadmin` | Local MinIO root password (**secret**). |
| `MINIO_BUCKET` | `task-attachments` | Bucket initialized by `minio-init`. |
| `MINIO_API_PORT` | `9000` | Host port mapped to the MinIO S3 API. |
| `MINIO_CONSOLE_PORT` | `9001` | Host port mapped to the MinIO console. |
| `BACKEND_HTTP_PORT` | `5000` | Host port mapped to the API container. |
| `FRONTEND_HTTP_PORT` | `3000` | Host port mapped to the frontend container. |

Changing a host port changes the URL used from the host, not the internal
container-to-container address. For example, the API still listens on
`backend:5000` inside the Compose network.

### ASP.NET Core and database

| Variable | Default/example | Purpose |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment name. |
| `ASPNETCORE_URLS` | `http://+:5000` | Listener inside the backend container. |
| `DEFAULT_CONNECTION` | Compose PostgreSQL connection string | Maps to `ConnectionStrings:DefaultConnection`. **Secret**. |
| `DATABASE_APPLY_MIGRATIONS_ON_STARTUP` | environment-specific | Optional `Database:ApplyMigrationsOnStartup` override. Local development may apply migrations at startup; production uses the separate migration container. |

The connection string must point to `db` from inside Compose. A host-run API
should use `localhost` and the published `POSTGRES_PORT` instead.

### JWT, cookies, and Data Protection

| Variable | Default/example | Purpose |
| --- | --- | --- |
| `JWT_SECRET` | local-only example | `Jwt:Secret`; use at least 64 random characters in production. **Secret**. |
| `JWT_ISSUER` | `Dotnet-React-Starter-API` | JWT issuer. |
| `JWT_AUDIENCE` | `Dotnet-React-Starter-App` | JWT audience. |
| `JWT_ACCESS_TOKEN_EXPIRES_IN_MINUTES` | `15` | Access-token lifetime. |
| `JWT_REFRESH_TOKEN_EXPIRES_IN_DAYS` | `7` | Refresh-token lifetime. |
| `JWT_REFRESH_TOKEN_COOKIE_NAME` | `drs.refreshToken` | Refresh-token cookie name. |
| `JWT_REFRESH_TOKEN_COOKIE_PATH` | `/api/auth` | Cookie path. |
| `JWT_REFRESH_TOKEN_COOKIE_SAME_SITE` | `Lax` | SameSite policy. |
| `JWT_REFRESH_TOKEN_COOKIE_SECURE_POLICY` | `SameAsRequest` | Secure-cookie policy; production must use `Always`. |
| `JWT_REFRESH_TOKEN_COOKIE_IS_ESSENTIAL` | `true` | Whether the refresh cookie is marked essential. |
| `JWT_REFRESH_TOKEN_COOKIE_DOMAIN` | empty | Optional cookie domain. Keep empty unless a deliberate shared-domain design requires it. |
| `DATA_PROTECTION_KEY_RING_PATH` | `/home/app/.aspnet/DataProtection-Keys` | `DataProtection:KeyRingPath`. The directory must be persistent and protected. |
| `DATA_PROTECTION_APPLICATION_NAME` | `DotnetReactStarter` | `DataProtection:ApplicationName`; keep stable for a shared key ring. |

### Proxy and CORS

| Variable | Default/example | Purpose |
| --- | --- | --- |
| `FORWARDED_HEADERS_ENABLED` | `false` | Trust forwarded headers only when the proxy/network is explicitly known. |
| `FORWARDED_HEADERS_KNOWN_NETWORK_0` | `172.28.0.0/16` | Trusted proxy network in local Compose. |
| `FORWARDED_HEADERS_FORWARD_LIMIT` | `1` | Maximum forwarded-header hops. |
| `CORS_ALLOW_CREDENTIALS` | `true` | Allows credentialed browser requests where configured. |
| `CORS_ALLOWED_ORIGIN_0` | `http://localhost:3000` | First allowed browser origin. |
| `CORS_ALLOWED_ORIGIN_1` | `http://localhost:5173` | Optional alternate Vite development origin. |
| `CORS_ALLOWED_ORIGIN_2` | `https://localhost:5173` | Optional HTTPS alternate Vite origin. |

Do not use `*` with credentialed requests. In production, allow only the
public HTTPS origin.

### Email confirmation, 2FA, and delivery

| Variable | Default/example | Purpose |
| --- | --- | --- |
| `EMAIL_CONFIRMATION_PUBLIC_ORIGIN` | `http://localhost:3000` | Public origin used when building confirmation links. |
| `EMAIL_CONFIRMATION_CONFIRMATION_PATH` | `/confirm-email` | Frontend confirmation route. |
| `EMAIL_CONFIRMATION_TOKEN_EXPIRES_IN_HOURS` | `24` | Confirmation-token lifetime. |
| `EMAIL_TWO_FACTOR_ENABLED` | `true` | Enables email 2FA support. |
| `EMAIL_TWO_FACTOR_ENABLE_FOR_NEW_USERS` | `true` | Default email-2FA preference for new accounts. |
| `EMAIL_TWO_FACTOR_CODE_EXPIRES_IN_MINUTES` | `10` | Email 2FA code lifetime. |
| `EMAIL_TWO_FACTOR_CODE_LENGTH` | `6` | Generated email 2FA code length. |
| `EMAIL_TWO_FACTOR_MAX_FAILED_ATTEMPTS` | `5` | Maximum failed attempts for a challenge. |
| `EMAIL_DELIVERY_ENABLED` | `true` | Enables delivery through the configured sender. |
| `EMAIL_DELIVERY_HOST` | `mailpit` | SMTP host inside Compose; use the provider host in production. |
| `EMAIL_DELIVERY_PORT` | `1025` | SMTP port. |
| `EMAIL_DELIVERY_TIMEOUT_SECONDS` | `30` | SMTP operation timeout. |
| `EMAIL_DELIVERY_FROM_ADDRESS` | local example | Sender address. |
| `EMAIL_DELIVERY_FROM_NAME` | `Dotnet React Starter` | Sender display name. |
| `EMAIL_DELIVERY_USERNAME` | empty | SMTP username (**secret when used**). |
| `EMAIL_DELIVERY_PASSWORD` | empty | SMTP password (**secret when used**). |
| `EMAIL_DELIVERY_USE_START_TLS` | `false` | Enables STARTTLS for SMTP. Production providers normally require `true`. |

Mailpit is a local sink. It is not a production mail service and must not
receive production credentials or messages.

### Attachments and malware scanning

| Variable | Default/example | Purpose |
| --- | --- | --- |
| `ATTACHMENTS_STORAGE_PROVIDER` | `S3` | `Attachments:StorageProvider`; local Compose uses MinIO. |
| `ATTACHMENTS_ROOT_PATH` | `/app/data/task-attachments` | Filesystem root when filesystem storage is selected. |
| `ATTACHMENTS_S3_BUCKET_NAME` | `task-attachments` | Object-storage bucket. |
| `ATTACHMENTS_S3_REGION` | `us-east-1` | S3-compatible region value. |
| `ATTACHMENTS_S3_SERVICE_URL` | `http://minio:9000` | S3-compatible endpoint. |
| `ATTACHMENTS_S3_FORCE_PATH_STYLE` | `true` | Required by the local MinIO layout. |
| `ATTACHMENTS_S3_ACCESS_KEY` | local example | Application storage credential (**secret**). |
| `ATTACHMENTS_S3_SECRET_KEY` | local example | Application storage secret (**secret**). |
| `ATTACHMENTS_REQUIRE_MALWARE_SCAN` | `false` locally | Rejects unscanned attachments when enabled; production sets `true`. |
| `ATTACHMENTS_MALWARE_SCANNER_HOST` | empty locally | ClamAV host, normally `clamav` in production Compose. |
| `ATTACHMENTS_MALWARE_SCANNER_PORT` | `3310` | ClamAV daemon port. |
| `ATTACHMENTS_MALWARE_SCANNER_TIMEOUT_SECONDS` | `30` | Scan operation timeout. |

### Frontend build arguments

| Variable | Default/example | Purpose |
| --- | --- | --- |
| `FRONTEND_REACT_APP_API_URL` | `/api` | Vite API base URL passed to the frontend image build. |
| `FRONTEND_NODE_OPTIONS` | `--max_old_space_size=4096` | Node.js build memory setting. |

`/api` is the correct value when Caddy/Nginx serves the frontend and proxies
the API under the same public origin. A standalone Vite process should use
`VITE_API_URL=http://localhost:5031` (or the actual API URL).

## VPS staging and production variables

The deployment files fail fast for values that must be supplied. Start from
[`.env.production.example`](../deploy/vps/.env.production.example), replace
every placeholder, and keep the file outside source control.

| Variable | Required | Purpose |
| --- | --- | --- |
| `BACKEND_IMAGE` | yes | GHCR backend image repository. |
| `FRONTEND_IMAGE` | yes | GHCR frontend image repository. |
| `IMAGE_TAG` | yes | Full immutable commit SHA. Do not deploy a floating tag. |
| `APP_DOMAIN` | yes | Public HTTPS hostname. |
| `TLS_EMAIL` | yes | Caddy/ACME certificate contact address. |
| `POSTGRES_DB` | yes | Production database name. |
| `POSTGRES_USER` | yes | Production database user. |
| `POSTGRES_PASSWORD` | yes | Production database password. **Secret**. |
| `JWT_SECRET` | yes | Production JWT signing secret. **Secret**. |
| `JWT_ISSUER` / `JWT_AUDIENCE` | optional | Stable JWT metadata values. |
| `DATA_PROTECTION_APPLICATION_NAME` | optional | Stable Data Protection application name. |
| `MINIO_ROOT_USER` | yes | MinIO administrative user. **Secret-like credential**. |
| `MINIO_ROOT_PASSWORD` | yes | MinIO administrative password. **Secret**. |
| `MINIO_APP_ACCESS_KEY` | yes | Least-privilege backend storage user. **Secret**. |
| `MINIO_APP_SECRET_KEY` | yes | Backend storage secret. **Secret**. |
| `SMTP_HOST` / `SMTP_PORT` | yes/optional | External SMTP provider and port. |
| `SMTP_TIMEOUT_SECONDS` | optional | SMTP timeout; default `30`. |
| `SMTP_FROM_ADDRESS` | yes | Verified sender address. |
| `SMTP_FROM_NAME` | optional | Sender display name. |
| `SMTP_USERNAME` / `SMTP_PASSWORD` | optional | Provider credentials. **Secrets**. |
| `SMTP_USE_STARTTLS` | optional | Defaults to `true` in production Compose. |
| `GRAFANA_ADMIN_USER` | optional | Grafana administrator name. |
| `GRAFANA_ADMIN_PASSWORD` | yes | Grafana administrator password. **Secret**. |
| `ALERTMANAGER_WEBHOOK_URL` | yes | Private alert delivery endpoint. **Sensitive**. |
| `MAILPIT_HTTP_PORT` | staging only | Loopback-only Mailpit port when the staging profile is enabled. |
| `PUBLIC_HTTP_PORT` / `PUBLIC_HTTPS_PORT` | optional | Host ports exposed by Caddy; defaults are `80` and `443`. |

Production Compose derives additional settings rather than accepting separate
variables: it disables backend startup migrations, uses HTTPS-only cookies,
allows only `https://${APP_DOMAIN}`, enables S3 attachments, requires ClamAV,
and connects the backend to the private internal network.

## Validation checklist

Before starting a deployment, validate the interpolated configuration without
printing secret values into tickets or logs:

```powershell
docker compose --env-file .env config
```

Then verify:

- no placeholder values such as `replace-with-` remain;
- `APP_DOMAIN`, CORS, and email-confirmation origin use the same public HTTPS
  host;
- the Data Protection and PostgreSQL/MinIO volumes are persistent;
- production does not expose internal services publicly;
- the backend image tag is the intended full commit SHA;
- the migration container is run and completes before the backend is promoted.
