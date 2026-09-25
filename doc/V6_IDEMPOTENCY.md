# V6 Notification Idempotency

## Scope

This slice makes one existing operation explicitly idempotent:
`DatabaseNotificationWriter.CreateAsync`.

The contract is intentionally narrow. It applies only when the caller supplies a
stable `deduplicationKey` for one business event:

```text
(recipient user id, deduplication key) -> one Notification and at most one email outbox message
```

The key is not a request identifier for the whole API. It belongs to the
business event being notified, for example:

```text
task:{taskId}:comment:{commentId}
task:{taskId}:assigned
```

Callers must not generate a new random key for every retry. A retry must reuse
the key of the original event.

## Behavior

1. The writer normalizes and validates the key.
2. It checks for an existing notification for the same recipient and key.
3. If one exists, the operation returns successfully without creating a new
   notification or email outbox record.
4. PostgreSQL also enforces the invariant with the unique filtered index
   `IX_Notifications_UserId_DeduplicationKey`.
5. If two requests pass the existence check concurrently, the request that
   loses the unique-index race treats that specific constraint violation as an
   idempotent success. Other database errors are still propagated.

The notification and its optional email outbox record are persisted in the same
`SaveChangesAsync` transaction. A repeated key therefore cannot create a second
email outbox record after the first notification has been committed.

## Evidence

[`PostgreSqlNotificationIdempotencyTests`](../backend/IntegrationTests/PostgreSqlNotificationIdempotencyTests.cs)
uses the real PostgreSQL Testcontainer and separate dependency-injection scopes
for two calls with the same recipient and key. It verifies:

- exactly one notification exists;
- the first notification data is retained;
- exactly one email outbox message exists;
- the outbox message points to the retained notification.

The existing unit test covers the fast duplicate path. The PostgreSQL test
covers persistence and the database-backed uniqueness contract.

## Boundaries and follow-up

- This does not add a global HTTP `Idempotency-Key` middleware.
- This does not persist arbitrary HTTP responses or raw invitation tokens.
- The deduplication record is retained with the notification; cleanup/retention
  is a separate policy decision.
- Email delivery remains at-least-once at the external provider boundary. The
  outbox prevents duplicate enqueueing for one notification, but a process crash
  after an SMTP/API send and before marking the outbox row processed can still
  cause a provider-level duplicate. Provider idempotency or delivery receipts
  are required to solve that separate problem.
