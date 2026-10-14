# Security Policy

## Scope

This repository is a production-minded reference starter, not a security
certification or a complete application security program. The default Docker
Compose stack is intended for local development and testing only. Before
deploying a consumer application, review the authentication model, threat
model, data-retention requirements, hosting controls, and operational runbooks
for that application.

The security-sensitive baseline is documented in:

- [Configuration reference](doc/CONFIGURATION_REFERENCE.md);
- [JWT and authentication architecture](doc/JWT_ARCHITECTURE.md);
- [Email and 2FA flows](doc/EMAIL_2FA_FLOWS.md);
- [Attachment operations contract](doc/ATTACHMENT_OPERATIONS.md);
- [VPS deployment runbook](doc/V5_DEPLOYMENT_RUNBOOK.md).

## Reporting a vulnerability

Please do not disclose a suspected vulnerability in a public issue, pull
request, discussion, or commit.

Use GitHub's private vulnerability reporting or Security Advisories for this
repository when that capability is available. If private reporting is not
available, contact the repository maintainers through a private GitHub channel
and include the repository name and affected commit. Do not include production
secrets, personal data, or live customer information in the report.

Include, where possible:

1. a short description of the issue and its security impact;
2. the affected version, commit, endpoint, component, or deployment mode;
3. deterministic reproduction steps or a minimal proof of concept;
4. preconditions, required permissions, and whether exploitation is remote;
5. suggested mitigation, if known.

The maintainers will acknowledge a valid private report, reproduce it in a
non-production environment, assess severity, and coordinate remediation and
disclosure. Do not test against infrastructure or data that you do not own or
have explicit permission to assess.

## Supported security baseline

The repository follows the latest maintained development line and the latest
tagged release as the practical security baseline. Consumers should update
promptly when a dependency, base image, SDK, or workflow security fix is
released. Historical tags are useful for archaeology but should not be treated
as supported production baselines without an independent review.

## Required security practices

### Secrets and configuration

- Never commit passwords, JWT secrets, SMTP credentials, MinIO keys, webhook
  URLs, Data Protection keys, recovery codes, or real user data.
- Replace every example secret before deploying outside local development.
- Store production secrets in the deployment secret store or protected host
  environment, not in a tracked `.env` file.
- Keep the Data Protection key ring persistent and protected. Rotating or
  deleting it invalidates cookies and other protected values.
- Use a long, random `JWT_SECRET`; the local example value is intentionally not
  suitable for production.

### Authentication and authorization

- Keep access tokens short-lived and preserve refresh-token rotation and
  revocation semantics.
- Keep refresh tokens in the configured HttpOnly cookie. Do not move them to
  browser local storage.
- Use HTTPS in every non-local environment and set the production cookie policy
  to `Secure`/`Always`.
- Preserve email confirmation, account lockout, rate limiting, and neutral
  responses for account-enumeration-sensitive flows.
- Enforce authorization in the backend. Frontend feature flags are not access
  control.
- Treat authenticator recovery codes and password-reset links as credentials.
  Show them once, store them securely, and never log them.

### Data and attachments

- Use PostgreSQL credentials and object-storage credentials with the smallest
  practical scope.
- Keep private attachment buckets private and require malware scanning in
  production.
- Validate file size, content type, storage key, access rights, and scan result
  at the backend boundary.
- Protect database, object-storage, backup, and monitoring data with access
  controls and an appropriate retention policy.
- Do not expose Mailpit, MinIO, PostgreSQL, Prometheus, Grafana, or Alertmanager
  directly to the public Internet.

### Delivery and operations

- Review dependency and container scan findings before release.
- Deploy immutable image tags based on a full commit SHA.
- Run the production migration container separately and verify its exit status.
- Keep Caddy/TLS, host firewall, Docker Engine, operating system, and backup
  procedures maintained.
- Monitor readiness, worker, storage, malware-scanner, email, and metrics
  signals after deployment.
- Preserve `X-Correlation-ID` values when investigating incidents, but redact
  credentials, tokens, reset links, and personal data from shared diagnostics.

## Security changes in pull requests

Security-sensitive changes must include:

- the threat or failure mode being addressed;
- the affected trust boundary and authorization rule;
- tests for allowed and denied paths;
- configuration and migration impact;
- operational or rollback notes when deployment behavior changes.

Do not weaken validation, logging redaction, cookie settings, rate limiting,
authorization, or malware-scanning requirements merely to make a local test
pass. If a deliberate trade-off is necessary, document it in the pull request
and in the nearest durable documentation.

## Disclosure and limitations

The project may contain known limitations appropriate for a starter. A
successful CI run is not proof that a deployment is secure. Consumers remain
responsible for application-specific threat modeling, privacy obligations,
penetration testing, dependency maintenance, secret rotation, incident
response, and regulatory requirements.
