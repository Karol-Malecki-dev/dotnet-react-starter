# V5 Release Gate

V5 is accepted only after both repository validation and target-environment validation.

## Repository gate

Run:

```powershell
.\scripts\Invoke-E2ETests.ps1
```

CI additionally validates:

- production Docker Compose interpolation;
- Bash deployment, rollback, backup, and restore syntax;
- Bash staging verification and encrypted off-host backup transfer syntax;
- Bash automated release-evidence capture syntax;
- Caddy TLS/proxy configuration;
- Prometheus scrape and alert rules plus Alertmanager routing configuration;
- backend and frontend image builds;
- HIGH and CRITICAL image vulnerabilities before publication.

## Staging gate

Deploy an immutable commit-SHA image through the protected `staging` GitHub Environment.

Required evidence:

- migration container completed successfully;
- public `/health/live`, `/health/ready`, and `/health/workers` return HTTP 200;
- automatic TLS certificate is valid for the configured domain;
- registration, email confirmation, login, project, task, comment, and attachment workflows pass;
- Grafana receives application and host data;
- all configured Prometheus alert rules are loaded and Alertmanager delivers a test notification;
- a backup is copied off host;
- the same backup is restored on staging or an isolated clone;
- manual rollback restores the previous image and passes readiness.

## Production decision

Do not promote when any of the following is true:

- the target runtime or framework is outside vendor support;
- staging differs from production in cookie, TLS, database, storage, or migration behavior;
- the latest backup has not passed checksum verification;
- no previous immutable image is available;
- required GitHub Environment approval or host-key pinning is missing;
- image scanning reports an unresolved HIGH or CRITICAL vulnerability.

The repository provides three operator helpers:

- `deploy/vps/verify-staging.sh` checks the public HTTPS health endpoints, local
  Prometheus and Alertmanager readiness, Grafana health, loaded alert rules, and
  live node/application probe data. With `--trigger-alert-test`, it submits a
  synthetic alert to Alertmanager; the operator must still confirm delivery at the
  configured receiver.
- `deploy/vps/copy-backup-offhost.sh` copies a `*.tar.gz.gpg` archive to an SSH
  destination and compares the local SHA-256 checksum with the remote checksum.
- `deploy/vps/capture-release-evidence.sh` captures automated staging evidence,
  including public observability checks, Compose state, immutable deployed/previous
  tags, migration exit status, and a SHA-256 manifest. On a first deployment it
  records `AUTOMATED_CHECKS_PASSED_ROLLBACK_PENDING`; pass
  `--require-rollback-target` when validating release readiness so a missing
  rollback target fails the command.

The evidence bundle is not a release approval by itself. The operator must append
the manual backup, restore, Alertmanager delivery, and rollback results before
promoting or tagging V5.
