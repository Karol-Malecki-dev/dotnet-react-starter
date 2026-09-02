# V5 Deployment and Operations Runbook

## Deployment decision

V5 targets one Linux VPS running Docker Compose. This keeps the operational model proportional
to a single modular monolith while still providing TLS, immutable artifacts, controlled database
migrations, durable data, malware scanning, monitoring, backup, restore, and rollback.

Kubernetes is intentionally excluded. Moving to a managed database or external S3 provider later
does not change the application contracts.

## Production topology

- Caddy is the only public container and terminates TLS on ports `80` and `443`.
- The React nginx container and ASP.NET API are private.
- PostgreSQL and MinIO are private and persist data in named volumes.
- ClamAV scans every attachment before it becomes available.
- A one-shot migration container completes before the API starts.
- Data Protection keys persist across application replacements.
- Prometheus, Alertmanager, blackbox exporter, and node-exporter provide health, alert delivery,
  and host metrics.
- Grafana and Prometheus bind only to VPS loopback.

The deployment definition is `deploy/vps/compose.production.yml`.

## Host prerequisites

Use a currently supported Ubuntu LTS release with:

- at least 2 vCPU, 8 GB RAM, and sufficient SSD space for the database, ClamAV signatures,
  object storage, metrics, and two backup generations;
- Docker Engine and the Docker Compose plugin from Docker's official repository;
- `gnupg` for encrypted backup archives;
- a dedicated `dotnet-react` deployment account;
- inbound ports `80/tcp` and `443/tcp+udp`;
- SSH restricted to the operator's source network;
- DNS `A`/`AAAA` records for the selected application domain.

Membership in the Docker group is root-equivalent. Do not grant it to application users or
untrusted operators.

Recommended host paths:

```text
/opt/dotnet-react-starter
/etc/dotnet-react-starter/production.env
/var/backups/dotnet-react-starter
```

The environment file must be owned by `root:dotnet-react`, mode `0640`, and created from
`deploy/vps/.env.production.example`. Never copy the completed file into the repository,
container image, CI log, or support ticket.

Install a separate encryption key readable by the backup service account:

```bash
sudo install -o root -g dotnet-react -m 0640 \
  /secure-source/dotnet-react-backup-encryption.key \
  /etc/dotnet-react-starter/backup-encryption.key
```

Keep the key outside the backup destination and maintain an independent recovery copy. The
encrypted backup is not useful without this key, while the key alone does not contain application
data.

## GitHub staging environment

Create a protected GitHub Environment named `staging` and require manual approval.

Environment variables:

| Name | Example |
|---|---|
| `STAGING_SSH_HOST` | `203.0.113.10` |
| `STAGING_SSH_USER` | `dotnet-react` |
| `STAGING_APP_PATH` | `/opt/dotnet-react-starter` |
| `STAGING_ENV_FILE` | `/etc/dotnet-react-starter/production.env` |
| `STAGING_DOMAIN` | `staging.example.com` |

Environment secrets:

| Name | Purpose |
|---|---|
| `STAGING_SSH_PRIVATE_KEY` | Dedicated deployment key with no unrelated host access |
| `STAGING_SSH_KNOWN_HOSTS` | Pinned host key produced during trusted provisioning |
| `STAGING_GHCR_READ_TOKEN` | Fine-grained token with read-only package access |

Do not use `ssh-keyscan` during deployment. Host identity must already be pinned.

The protected staging environment uses the `staging` Compose profile. Its deployment environment
file must route SMTP to the profile's private Mailpit service:

```text
SMTP_HOST=mailpit
SMTP_PORT=1025
SMTP_USE_STARTTLS=false
```

Mailpit's HTTP API is bound to VPS loopback only. CD reaches it through an SSH tunnel while the
browser suite uses the public HTTPS domain. Production deployments must omit the `staging` profile
and use a real external SMTP provider.

Set `ALERTMANAGER_WEBHOOK_URL` in the protected environment file to the operator notification
endpoint. Do not put a webhook token or provider credential in the repository.

## First deployment

1. Merge a commit that passed all CI checks into `main`.
2. Wait for CD to publish backend and frontend images tagged with the full commit SHA.
3. Run `CD - Publish and deploy` manually from `main` with `deploy_staging=true`.
4. Approve the protected `staging` environment. The deployment starts the `staging` Compose
  profile so browser tests can inspect email without sending messages to real recipients.
5. Confirm `https://<domain>/health/ready` returns HTTP 200.
6. CD opens a pinned SSH tunnel to Mailpit and runs the registration, email confirmation, login,
  2FA, project, task, comment, and attachment browser smoke workflows through public HTTPS.
7. Verify that the Grafana dashboard receives health and host metrics and send a test
  Alertmanager notification.

The deployment script serializes deploys with `flock`. It records the active and previous image
tags and automatically rolls back the application when Compose or public readiness fails.

## Database migrations

The normal production API process must use:

```text
Database__ApplyMigrationsOnStartup=false
```

Only the one-shot container invokes the image with `--migrate-only`. The API depends on successful
completion of that container.

Every schema change must use expand-and-contract:

1. add backward-compatible schema;
2. deploy code that can use old and new representations;
3. backfill separately when required;
4. remove obsolete schema only in a later release.

Application rollback never executes an automatic EF Core down migration. If a migration is not
backward compatible, stop the release and restore the tested backup instead.

## Backup installation

Install the timer after copying the deployment directory:

```bash
sudo chown dotnet-react:dotnet-react \
  /opt/dotnet-react-starter/backup.sh \
  /opt/dotnet-react-starter/restore.sh
sudo chmod 0750 \
  /opt/dotnet-react-starter/backup.sh \
  /opt/dotnet-react-starter/restore.sh
sudo install -o root -g root -m 0644 \
  /opt/dotnet-react-starter/systemd/dotnet-react-backup.service /etc/systemd/system/
sudo install -o root -g root -m 0644 \
  /opt/dotnet-react-starter/systemd/dotnet-react-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now dotnet-react-backup.timer
```

The daily snapshot contains:

- a GPG-encrypted archive containing a PostgreSQL custom-format dump;
- an object-level MinIO export;
- the Data Protection key ring;
- image metadata;
- a SHA-256 manifest verified before encryption.

The script briefly stops Caddy, frontend, and backend to prevent cross-store writes during the
snapshot. PostgreSQL, MinIO, monitoring, and ClamAV remain running.

Copy the resulting `*.tar.gz.gpg` snapshots to encrypted off-host storage. The local GPG layer
protects the artifact before transfer; off-host storage still needs independent access control,
retention and encryption. A backup existing only on the application VPS is not disaster recovery.

## Restore drill

`restore.sh` is destructive and requires `--force`. Perform the first drill on staging or a cloned
VPS, never for the first time during a production incident.

```bash
cd /opt/dotnet-react-starter
./restore.sh \
  /etc/dotnet-react-starter/production.env \
  /var/backups/dotnet-react-starter/2026-08-31T020000Z.tar.gz.gpg \
  --force \
  https://staging.example.com \
  /etc/dotnet-react-starter/backup-encryption.key \
  staging
```

The script verifies the encrypted archive and its internal SHA-256 manifest, restores all three
data stores, runs the controlled migration container for the current image, and requires public
`/health/live`, `/health/ready`, and `/health/workers` checks to pass before returning success.

After the automated validation:

1. verify `/health/ready` and `/health/workers`;
2. authenticate using a session created before the snapshot;
3. download an attachment created before the snapshot;
4. create and delete a new attachment;
5. run attachment reconciliation and investigate any drift;
6. record restore duration and snapshot identifier.

Run a restore drill at least quarterly and before changing the database or storage topology. The
restore script performs migration and public health validation, but the operator must still run
the authenticated attachment reconciliation and record the result.

## Monitoring

Create a local tunnel:

```bash
ssh -L 3001:127.0.0.1:3001 -L 9090:127.0.0.1:9090 dotnet-react@HOST
```

- Grafana: `http://localhost:3001`
- Prometheus alerts: `http://localhost:9090/alerts`
- Alertmanager: `http://localhost:9093`

Configured alerts cover:

- process liveness;
- database, object storage, and ClamAV readiness;
- object storage, ClamAV, and recorded SMTP delivery failures;
- background worker health;
- low disk space;
- low available memory.

Container logs use bounded JSON rotation. Inspect them with:

```bash
docker compose --env-file /etc/dotnet-react-starter/production.env \
  -f /opt/dotnet-react-starter/compose.production.yml \
  logs --since 30m --no-color backend caddy
```

## Incident procedures

### API not ready

1. Check `/health/live`, `/health/ready`, and Prometheus alerts.
2. Inspect `db`, `minio`, `clamav`, and `backend` health.
3. Use correlation IDs to join Caddy and API logs.
4. Do not restart repeatedly before preserving logs.

### Database unavailable

1. Stop public writes by stopping `caddy`.
2. Check disk space and PostgreSQL logs.
3. Restart only after identifying storage or configuration failure.
4. Restore the latest verified snapshot if data files are damaged.

### Storage or scanner unavailable

Readiness becomes unhealthy and attachment uploads fail closed. Do not disable malware scanning
to restore availability. Repair MinIO/ClamAV, update signatures, and rerun attachment smoke tests.

### Email unavailable

Authentication emails can fail while readiness remains healthy. Inspect SMTP provider status and
email delivery logs. Rotate credentials in the protected environment file and recreate `backend`.

### Worker unhealthy

Inspect `/health/workers` and backend logs. Verify database connectivity and the last successful
worker cycle before restarting the API.

## Manual rollback

The deploy script rolls back automatically on startup or smoke-test failure. For a later regression:

```bash
cd /opt/dotnet-react-starter
./rollback.sh \
  /etc/dotnet-react-starter/production.env \
  https://staging.example.com
```

Rollback changes application images only. It does not reverse database migrations.

## Secret rotation

1. Generate the new secret outside the repository.
2. Update `/etc/dotnet-react-starter/production.env` atomically.
3. Recreate only affected services.
4. Verify readiness and the affected workflow.
5. Revoke the old credential after verification.

Rotating the JWT secret invalidates access tokens. Rotating Data Protection keys by deleting the
key ring invalidates protected tokens and is not a normal rotation procedure.

## Release checklist

- CI, image scans, and deployment configuration checks are green.
- Deployment uses a full commit SHA or reviewed semantic version tag.
- Staging approval is recorded.
- Migration review confirms backward compatibility.
- Public TLS, HSTS, cookies, CORS, and email links use the target domain.
- Readiness, worker health, dashboard, all configured alert rules, and the Alertmanager delivery
  route are operational.
- Off-host backup completed and a restore drill has a recorded result.
- Browser smoke workflows pass after deployment.
- Previous image tag is available for rollback.
- No secrets appear in repository changes, image history, or logs.
