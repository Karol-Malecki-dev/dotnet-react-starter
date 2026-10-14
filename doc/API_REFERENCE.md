# API Reference

This is a route-oriented reference for the current ASP.NET Core API. The
controllers and transport contracts remain the source of truth; update this
page when a public route, request contract, authorization rule, or response
shape changes.

## Base URLs

| Mode | Base URL |
| --- | --- |
| Root Docker Compose | `http://localhost:5000` |
| Backend launch profile outside Compose | `http://localhost:5031` |
| Public deployment | `https://<APP_DOMAIN>` |

Swagger is enabled only in the Development environment. The API also exposes
the manual request file at [backend/API/API.http](../backend/API/API.http).

## Request and response conventions

Authenticated requests normally send:

```http
Authorization: Bearer <access-token>
Content-Type: application/json
```

Browser clients must send credentials for refresh-token cookie operations. The
refresh token is an HttpOnly cookie and must not be copied into JavaScript
storage or an `Authorization` header.

Most JSON endpoints use the following envelope:

```json
{
  "statusCode": 200,
  "message": "Success",
  "data": {},
  "errors": null,
  "timestamp": "2026-01-01T12:00:00Z"
}
```

Error details, when present, have `field`, `message`, and optional `code`
properties:

```json
{
  "statusCode": 400,
  "message": "Invalid request data",
  "data": null,
  "errors": [
    {
      "field": "email",
      "message": "Email is required",
      "code": "NotEmptyValidator"
    }
  ],
  "timestamp": "2026-01-01T12:00:00Z"
}
```

Every API response receives an `X-Correlation-ID` header. Preserve it when
reporting a failure. Status codes are part of the contract: `400` means an
invalid request or domain input, `401` means missing/invalid authentication,
`403` means authenticated but forbidden, `404` means missing resource, and
`409` means a concurrency or state conflict.

Enum fields currently use the configured System.Text.Json representation. The
examples below use the numeric values from the current contracts:

| Enum | Values |
| --- | --- |
| `ResetType` | `Link = 0`, `Code = 1` (the current password-reset endpoints support `Link`) |
| `ProjectMemberRole` | `Owner = 1`, `Member = 2`, `Viewer = 3` |
| `ProjectTaskStatus` | `Todo = 1`, `InProgress = 2`, `Done = 3` |
| `ProjectTaskPriority` | `Low = 1`, `Normal = 2`, `High = 3` |

## Authentication and account lifecycle

All paths below are under `/api/auth`.

| Method and route | Auth | Purpose |
| --- | --- | --- |
| `POST /register` | Anonymous, rate limited | Creates an account and sends confirmation email. |
| `POST /confirm-email` | Anonymous, rate limited | Consumes a single-use confirmation token. |
| `POST /resend-confirmation` | Anonymous, rate limited | Sends a neutral confirmation response. |
| `POST /login` | Anonymous, rate limited | Validates credentials; returns tokens or starts 2FA. |
| `POST /verify-2fa` | Anonymous, rate limited | Completes an email or authenticator challenge. |
| `POST /resend-2fa` | Anonymous, rate limited | Replaces an active email challenge. |
| `POST /authenticator/setup` | Authenticated | Starts authenticator-app setup. |
| `POST /authenticator/confirm` | Authenticated | Confirms a TOTP code and returns recovery codes once. |
| `POST /authenticator/disable` | Authenticated | Disables authenticator 2FA after re-authentication. |
| `POST /authenticator/recovery-codes` | Authenticated | Replaces authenticator recovery codes. |
| `POST /refresh-token` | Refresh cookie | Rotates the refresh token and returns a new access token. |
| `POST /logout` | Authenticated + refresh cookie | Revokes the current refresh session. |
| `POST /logout-all` | Authenticated | Revokes all refresh sessions for the current user. |
| `GET /me` | Authenticated | Returns the current user snapshot. |
| `POST /verify-token` | Anonymous, rate limited | Validates a supplied access token. |
| `POST /change-password` | Authenticated | Changes the current password. |
| `POST /forgot-password` | Anonymous, rate limited | Starts the link-based password-reset flow. |
| `POST /reset-password` | Anonymous, rate limited | Consumes a password-reset token. |

### Registration and login examples

```http
POST {{baseUrl}}/api/auth/register
Content-Type: application/json

{
  "email": "person@example.test",
  "password": "Use-a-development-only-password-123!",
  "firstName": "Ada",
  "lastName": "Lovelace",
  "phoneNumber": "",
  "address": ""
}
```

Successful registration returns `201` with `data.email` and
`data.requiresEmailConfirmation`. It does not issue an access token.

```http
POST {{baseUrl}}/api/auth/login
Content-Type: application/json

{
  "email": "person@example.test",
  "password": "Use-a-development-only-password-123!"
}
```

Without 2FA, a successful login returns `200` and
`data: { "accessToken", "expiresIn", "tokenType" }`, while setting the
refresh-token cookie. With 2FA, it returns `202` and
`data: { "requiresTwoFactor", "method", "challengeId", "destinationHint", "expiresAt" }`.
Submit that challenge identifier and the received code:

```http
POST {{baseUrl}}/api/auth/verify-2fa
Content-Type: application/json

{
  "challengeId": "00000000-0000-0000-0000-000000000000",
  "code": "123456"
}
```

Refresh and logout requests must preserve browser credentials:

```http
POST {{baseUrl}}/api/auth/refresh-token
```

```http
POST {{baseUrl}}/api/auth/logout
Authorization: Bearer <access-token>
```

### Password and authenticator payloads

```json
{
  "currentPassword": "current-development-password",
  "newPassword": "new-development-password"
}
```

The payload above is used by `/change-password`. The link-based reset flow
uses:

```json
{
  "email": "person@example.test",
  "resetType": 0,
  "token": "<single-use-token-from-email>",
  "newPassword": "new-development-password"
}
```

`/forgot-password` accepts the email and `resetType: 0`. The response is
intentionally neutral whether the account exists. Code-based reset is not
currently supported by these endpoints.

Authenticator setup returns a provisioning URI. `/authenticator/confirm`
accepts `{ "code": "123456" }` and returns recovery codes exactly once.
Disabling or regenerating codes accepts `{ "currentPassword": "...",
"code": "..." }`.

## User and administrator endpoints

### `/api/users`

| Method and route | Auth | Purpose |
| --- | --- | --- |
| `GET /api/users/me` | Authenticated | Returns the current user through the user service. |
| `PUT /api/users/me` | Authenticated | Updates the current profile. |
| `GET /api/users/me/security` | Authenticated | Returns current security preferences. |
| `PATCH /api/users/me/security/two-factor` | Authenticated | Changes email 2FA preference; body is `{ "enable": true }`. |
| `GET /api/users/{id}` | Admin | Returns a user by identifier. |
| `GET /api/users` | Admin | Lists users with `pageNumber` and `pageSize`. |
| `GET /api/users/count` | Admin | Returns the user count. |
| `DELETE /api/users/{id}` | Admin | Deletes a user. |
| `PUT /api/users/{id}/display-name` | Admin | Replaces a display name. |
| `PUT /api/users/{id}/role` | Admin | Changes a user role. |

### `/api/admin`

All `/api/admin` routes require the `Admin` role.

| Method and route | Purpose |
| --- | --- |
| `GET /api/admin/dashboard-stats` | Returns administrator dashboard statistics. |
| `GET /api/admin/users` | Filters/paginates administrative user data. |
| `GET /api/admin/security-events` | Lists account security events. |
| `GET /api/admin/users/{userId}` | Returns administrator details for one user. |
| `GET /api/admin/users/by-email?email=...` | Finds administrator details by email. |
| `PUT /api/admin/users/{userId}` | Updates an administrator user record. |
| `PUT /api/admin/users/{userId}/role` | Changes a user's role. |
| `PUT /api/admin/users/{userId}/activate` | Activates a user. |
| `PUT /api/admin/users/{userId}/deactivate` | Deactivates a user. |
| `DELETE /api/admin/users/{userId}` | Deletes a user. |

The exact filter and update DTOs are owned by the corresponding controller
contracts and should be changed together with this page.

## Projects and invitations

All project routes require authentication. Project-owner/member permissions
are enforced by the application handlers; a visible UI action is not an
authorization grant.

### Projects

| Method and route | Purpose |
| --- | --- |
| `GET /api/projects` | Lists projects visible to the current user. |
| `POST /api/projects` | Creates a project; body `{ "name": "...", "description": "..." }`. |
| `GET /api/projects/{projectId}` | Returns project details. |
| `PUT /api/projects/{projectId}` | Updates name/description and may include `concurrencyStamp`. |
| `DELETE /api/projects/{projectId}` | Archives a project. |
| `GET /api/projects/{projectId}/dashboard` | Returns project dashboard data. |
| `GET /api/projects/{projectId}/activity` | Lists project activity. |
| `GET /api/projects/{projectId}/members` | Lists project members. |
| `GET /api/projects/{projectId}/members/available` | Lists users available to add. |
| `POST /api/projects/{projectId}/members` | Adds a member; body `{ "userId": "<guid>" }`. |
| `PATCH /api/projects/{projectId}/members/{userId}/role` | Changes a member role. |
| `DELETE /api/projects/{projectId}/members/{userId}` | Removes a member. |
| `GET /api/projects/{projectId}/invitations` | Lists project invitations. |
| `POST /api/projects/{projectId}/invitations` | Creates an invitation for an existing user. |

Invitation creation accepts an email and `role` (`Member` or `Viewer` numeric
value). The response includes a one-time raw token for the owner to deliver
through the intended invitation channel.

### User invitation inbox

| Method and route | Purpose |
| --- | --- |
| `GET /api/project-invitations/mine` | Lists invitations addressed to the current user. |
| `POST /api/project-invitations/accept` | Accepts `{ "token": "<one-time-token>" }`. |
| `POST /api/project-invitations/decline` | Declines `{ "token": "<one-time-token>" }`. |

## Project tasks, comments, and attachments

### Tasks

| Method and route | Purpose |
| --- | --- |
| `GET /api/projects/{projectId}/tasks` | Lists paged tasks. |
| `POST /api/projects/{projectId}/tasks` | Creates a task. |
| `GET /api/projects/{projectId}/tasks/{taskId}` | Returns task details. |
| `PUT /api/projects/{projectId}/tasks/{taskId}` | Updates a task. |
| `PATCH /api/projects/{projectId}/tasks/{taskId}/status` | Changes task status. |
| `DELETE /api/projects/{projectId}/tasks/{taskId}` | Deletes a task. |

Task list query parameters are `pageNumber`, `pageSize`, `search`, `status`,
`priority`, `assignedUserId`, `label`, `dueBefore`, `sortBy`, and
`sortDirection`. The response contains `items`, `pageNumber`, `pageSize`, and
`totalCount`.

Create/update task bodies use:

```json
{
  "title": "Prepare release notes",
  "description": "Document the operational changes.",
  "priority": 2,
  "dueDate": "2026-02-01T12:00:00Z",
  "assignedUserId": null,
  "labels": ["release"],
  "concurrencyStamp": null
}
```

The `concurrencyStamp` is returned by the API and should be sent back by
clients when updating a resource that exposes it. A stale value can result in
`409 Conflict`.

### Comments and attachments

| Method and route | Purpose |
| --- | --- |
| `GET /api/projects/{projectId}/tasks/{taskId}/comments` | Lists task comments. |
| `POST /api/projects/{projectId}/tasks/{taskId}/comments` | Creates `{ "content": "..." }`. |
| `DELETE /api/projects/{projectId}/tasks/{taskId}/comments/{commentId}` | Deletes a comment. |
| `GET /api/projects/{projectId}/tasks/{taskId}/attachments` | Lists task attachments. |
| `POST /api/projects/{projectId}/tasks/{taskId}/attachments` | Uploads multipart field `file`. |
| `GET /api/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}/download` | Streams a private attachment. |
| `DELETE /api/projects/{projectId}/tasks/{taskId}/attachments/{attachmentId}` | Deletes an attachment. |

Uploads are `multipart/form-data`, limited to approximately 11 MiB at the HTTP
boundary, and are subject to storage and malware-scanning policy. Do not trust
the client-provided file name or content type.

## Notifications and workspace search

| Method and route | Auth | Purpose |
| --- | --- | --- |
| `GET /api/notifications?pageNumber=1&pageSize=20&unreadOnly=false` | Authenticated | Lists paged notifications. |
| `GET /api/notifications/unread-count` | Authenticated | Returns the unread count. |
| `PATCH /api/notifications/{id}/read` | Authenticated | Marks one notification as read. |
| `PATCH /api/notifications/read-all` | Authenticated | Marks all notifications as read. |
| `GET /api/notifications/email-preference` | Authenticated | Reads notification email preferences. |
| `PATCH /api/notifications/email-preference` | Authenticated | Updates nullable preference fields. |
| `GET /api/workspace/search?query=release&type=projectTask&page=1&pageSize=10` | Authenticated | Searches the current workspace. |

Workspace search requires a query between 2 and 100 characters. Notification
pages include `items`, `pageNumber`, `pageSize`, `totalCount`, and
`unreadCount`.

## Operational and public endpoints

| Method and route | Auth | Purpose |
| --- | --- | --- |
| `GET /health` | Anonymous | General application health. |
| `GET /health/live` | Anonymous | Liveness probe. |
| `GET /health/ready` | Anonymous | Readiness/dependency probe. |
| `GET /health/workers` | Anonymous | Background-worker health. |
| `GET /health/storage` | Anonymous | Attachment storage health. |
| `GET /health/malware-scanner` | Anonymous | ClamAV/malware scanner health. |
| `GET /health/email` | Anonymous | Email delivery health. |
| `GET /metrics` | Anonymous/internal | Prometheus-compatible metrics; restrict at the network boundary in production. |
| `GET /api/runtime-config` | Anonymous | Non-sensitive frontend feature and integration flags. |

Runtime configuration must never be used as an authorization decision. The
server-side authorization handlers remain authoritative.
