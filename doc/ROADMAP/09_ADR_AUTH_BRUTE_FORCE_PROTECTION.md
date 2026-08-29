# ADR-09: Authentication brute-force protection

- Status: Accepted
- Date: 2026-08-29
- Scope: authentication endpoints and password login state

## Implementation status

As of: **2026-08-29**.

| Decision area | Progress | Status |
|---|---:|---|
| Request rate limiting | 100% | Implemented, configured and covered by integration tests. |
| Password-login lockout | 100% | Implemented with persisted state, concurrency handling and tests. |
| Neutral public authentication contract | 100% | Invalid credentials and lockout responses remain neutral and are tested. |
| Validation and documentation | 100% | Options validation, ADR rationale and known limitations are documented. |

**Implementation progress for the current ADR scope: 100%**.

The limitations listed below are intentional follow-up decisions, not missing pieces of the accepted current scope.

## Context

The authentication surface contains public endpoints that can be abused for password
brute force, account enumeration, email delivery abuse, token guessing, and repeated
2FA attempts. A process-local request limit alone is not enough because it disappears
when the process restarts and does not protect an account from distributed attempts.
The application therefore needs two complementary controls:

1. a request rate limit for endpoint abuse;
2. a database-backed account lockout for repeated invalid passwords.

The controls must preserve the existing neutral public authentication contract and must
not persist or log passwords, raw tokens, or verification codes.

## Decision

### Request rate limiting

Use the built-in ASP.NET Core rate limiter with one fixed-window policy named
`AuthPolicy`:

- partition key: client IP address plus request path;
- default permit limit: 5 requests;
- default window: 60 seconds;
- queue limit: 0, so rejected requests fail immediately;
- response: neutral `429 Too Many Requests` using the existing `ApiResponse` contract.

The policy is applied to the public authentication endpoints:

- register;
- confirm and resend email confirmation;
- login;
- verify and resend email 2FA;
- refresh token;
- verify access token;
- forgot password;
- reset password.

The same policy also covers the authenticated credential and authenticator operations:

- change password;
- authenticator setup and confirmation;
- authenticator disable;
- recovery-code regeneration.

The values are configurable through `AuthSecurity` and validated when the application
starts. A different endpoint receives a different rate-limit bucket for the same IP,
so traffic to one flow does not consume the quota of another flow.

### Password-login lockout

Store the following state on `User`:

- `FailedLoginAttempts`;
- `LockoutEndAt`;
- `ConcurrencyStamp` as an EF Core optimistic concurrency token.

For an existing active account, every invalid password increments the consecutive-failure
counter. When `MaxFailedLoginAttempts` is reached, password login is blocked until
`LockoutEndAt`. A lockout expiration resets the state before the next password check.
A successful login resets both values. Missing and inactive accounts return the same
result as invalid credentials and do not create account-specific state.

The login controller continues to return neutral `401 Unauthorized` for an invalid
password, an unknown account, an inactive account, or an active lockout. This avoids
exposing the account state through the public response contract.

Authentication-state writes use `ConcurrencyStamp`. If two requests update the same
user concurrently, the losing update is rejected and does not overwrite the winner's
counter. The caller receives the same neutral authentication failure and can retry.

## Consequences

### Positive

- repeated password guesses eventually stop working for the affected account;
- endpoint abuse is limited before controller code executes;
- lockout state survives process restarts and is shared by application instances through
  the database;
- public responses do not disclose whether an email belongs to an account;
- configuration can be tuned without changing code;
- no additional package or external service is required.

### Negative and known limitations

- the in-process rate limiter is not shared between application instances;
- without correctly configured forwarded headers, all clients behind a reverse proxy can
  appear to use the proxy IP;
- attackers using many IP addresses can bypass an IP-only request quota, although the
  account lockout still protects password login;
- the response remains intentionally neutral, so the frontend cannot show an exact
  lockout time;
- timing differences between an unknown account and an existing account are not fully
  eliminated by this ADR;
- authenticated authenticator-management endpoints still require their own
  re-authentication rules in addition to the request limiter.

A distributed limiter, forwarded-header configuration, audit events, and a dedicated
lockout UX are follow-up work when the deployment model requires them.

## Persistence and migration

The lockout columns and user concurrency stamp are added by
`AddUserLoginLockout`. Existing rows receive safe defaults during migration. New users
receive an application-generated concurrency stamp.

## Validation

The implementation is covered by:

- unit tests for the lockout threshold, active lockout, expiration, successful-login
  reset, and unknown-account behavior;
- integration tests for neutral `401` responses, account lockout, login rate limiting,
  forgot-password rate limiting, refresh-token rate limiting, and access-token
  verification rate limiting;
- solution build and non-container integration tests.

PostgreSQL/Testcontainers validation additionally requires a running Docker Engine.
