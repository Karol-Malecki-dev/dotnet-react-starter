# V6 Notification Email Outbox Leasing

## Problem

The notification email outbox is durable, but a plain `SELECT` is not a claim.
Two application instances can read the same due row before either instance
marks it processed and can therefore send the same email concurrently.

The outbox still has an at-least-once delivery boundary. A process can send an
email and crash before recording the successful state. Leasing prevents
concurrent ownership; it cannot undo a delivery that already reached an
external provider.

## Lease contract

Each outbox row has two nullable fields:

- `ProcessingLeaseId` identifies the worker batch that currently owns the row;
- `ProcessingLeaseExpiresAt` is the UTC recovery deadline.

The processor uses the following contract:

1. Select at most 20 due rows whose lease is empty or expired.
2. Atomically claim the selected ids with a conditional PostgreSQL `UPDATE`.
3. Load only rows carrying the processor's lease id.
4. Send each email.
5. Mark success or record retry state only when the same lease id still owns
   the row.
6. Clear both lease fields after success or failure.
7. Allow another processor to reclaim a row after the five-minute lease expires.

The existing retry policy remains explicit:

- maximum of three attempts;
- after a failure, `NextAttemptAt` is delayed by the new attempt count in
  minutes;
- after the third failure the row remains unprocessed with its last error and
  is no longer selected by the worker.

The `ProcessedAt`, `NextAttemptAt` and `ProcessingLeaseExpiresAt` index supports
the due-row and lease-recovery lookup.

## Concurrency evidence

[`PostgreSqlNotificationEmailOutboxLeasingTests`](../backend/IntegrationTests/PostgreSqlNotificationEmailOutboxLeasingTests.cs)
uses a real PostgreSQL Testcontainer and two independent scoped processors. A
blocking sender holds the first delivery open while the second processor runs.
The test verifies that:

- exactly one processor sends the claimed row;
- the other processor finishes without sending it;
- the successful row has both lease fields cleared;
- an expired lease can be reclaimed;
- a failed delivery clears ownership and schedules the retry.

## Boundaries and follow-up

- This is database-level coordination for application instances using the same
  PostgreSQL database.
- It does not provide provider-level email idempotency.
- A crash after an external send and before the conditional success update can
  still cause a later retry and duplicate provider delivery.
- A durable dead-letter status, queue-lag metrics and provider delivery
  receipts remain follow-up work.
