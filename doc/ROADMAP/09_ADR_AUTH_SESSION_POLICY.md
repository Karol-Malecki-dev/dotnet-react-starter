# ADR: Authentication Session Policy

- Status: Accepted
- Date: 2026-08-22
- Scope: V2 session policy and refresh-token rotation hardening

## Context

The application uses short-lived JWT access tokens and database-backed refresh tokens stored as hashes. The refresh token is sent only in an HttpOnly cookie. A refresh-token row also contains a snapshot of user data for auditability, but that snapshot must not be treated as the current account state.

The main risks were stale account state, refresh-token reuse, two requests rotating one token at the same time, and old refresh sessions surviving a password change or reset.

## Decision

### Session rules

| Event | Refresh-session behavior | Access-token behavior |
| --- | --- | --- |
| Login or completed 2FA | Create a new refresh-token family. Existing families remain active. | Issue a new short-lived access token. |
| Single logout | Revoke only the refresh token from the current cookie with `UserLogout`. | The current access token remains valid until expiry. |
| Logout all | Revoke every active refresh token for the user with `UserLogout`. Clear the current cookie. | Existing access tokens remain valid until expiry. |
| Password change | Revoke every active refresh token with `PasswordChanged` in the same EF save operation as the password update. Clear the current cookie. | The access token used for the request remains valid until expiry. |
| Password reset | Revoke every active refresh token with `PasswordReset` in the same EF save operation as the password update. Clear the current cookie. | Existing access tokens remain valid until expiry. |
| Account deactivation | Login and refresh reject the account while `IsActive` is false. | Already issued access tokens remain valid until expiry. |
| Account activation | Login and refresh become eligible again when the account is active and email-confirmed. | No access token is created by activation. |
| Role change | The next refresh reads the current role from the database and places it in the successor access token. | Existing access tokens keep their old role until expiry. |
| 2FA setting change | The setting affects future login challenges. Refresh does not restart a 2FA challenge. | Existing access tokens remain valid until expiry. |

The access-token limitations are intentional for this slice. Immediate access-token invalidation requires a server-side token version, a deny-list, or an introspection step on protected requests. That is a separate trade-off and is not introduced here.

### Refresh rotation

1. Hash the supplied raw token and find its database row.
2. Reject missing, expired, revoked, missing-user, or inactive-user tokens.
3. Query the current `User` record before creating the successor pair.
4. Preserve the existing `FamilyId`, or create one for a legacy row without a family.
5. Mark the old row as `TokenRotated` and store `ReplacedByTokenHash`.
6. Add the successor row in the same `SaveChangesAsync` call.
7. Use `ConcurrencyStamp` as an EF Core concurrency token. A competing update loses with `DbUpdateConcurrencyException` and does not receive a successor.

A request that presents a token already marked `TokenRotated` is treated as replay. The active rows in that token family are revoked with `RefreshTokenReplay`, including the successor that may have been issued before the replay was observed.

### Logout-all endpoint

`POST /api/auth/logout-all` is authenticated with the access token. It obtains the user id from `sub` (with the existing name-identifier fallback), revokes all active refresh sessions, and clears the current refresh cookie. It does not promise immediate access-token invalidation.

### Storage and migration

`RefreshTokens.ConcurrencyStamp` is required, limited to 64 characters, and mapped as an EF Core concurrency token. Existing rows receive an empty-string migration default; the next successful rotation or revocation replaces it with a generated value. New rows always receive a generated value.

The schema change is delivered by migration `AddRefreshTokenConcurrencyStamp`.

## Consequences

### Positive

- A refresh cannot resurrect a deleted or inactive account.
- Successor tokens use current role, email, display name, and confirmation state.
- A rotated token is one-use and its family can be invalidated after replay.
- Parallel refresh requests produce at most one accepted successor.
- Password changes and resets terminate all refresh sessions in one EF save operation.
- Refresh-token hashes and replacement links provide an audit trail without storing raw refresh secrets.

### Costs and limitations

- Access tokens remain usable until their normal expiration after logout-all, password changes, resets, deactivation, role changes, and replay response.
- Deactivation is enforced at login and refresh boundaries; it does not rewrite existing refresh rows or revoke an already issued access token. If an account is reactivated, an unexpired refresh row can become eligible again. A future account-security policy may revoke refresh rows at deactivation time if reactivation must require a fresh login.
- The concurrency test is covered with separate EF contexts and the PostgreSQL migration is exercised through Testcontainers. A high-contention production test and metrics are still future work.
- Refresh-token rows are retained for audit and require a later cleanup/retention policy.

## Validation

The behavior is covered by:

- `JwtTokenServiceTests` for current user state, inactive accounts, rotation, replay family revocation, and concurrent refresh.
- `DatabaseAuthServiceTests` for password change/reset session invalidation.
- `AuthControllerTests` for logout-all and cookie clearing.
- `AuthApiIntegrationTests` for inactive accounts, role changes, password invalidation, replay, concurrent requests, and logout-all.
- `PostgreSqlIntegrationTests` for applying the complete migration set with PostgreSQL 16.

## Follow-up decisions

- Consider a user security-version claim or server-side token version if immediate access-token invalidation becomes required.
- Decide whether deactivation must revoke stored refresh rows so reactivation always requires a new login.
- Add rate-limit and lockout hardening for all public authentication flows.
- Define refresh-token retention and cleanup for revoked and expired rows.
