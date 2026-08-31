# Attachment Operations Contract

## Scope

Attachment metadata is stored in PostgreSQL and binary content is stored through
`IProjectTaskAttachmentStorage`. The Docker Compose deployment persists binaries in the
`attachment-files` volume. Production deployments must replace or durably mount that
storage; ephemeral container filesystems are unsupported.

## Retention

- Active attachment metadata and its binary have the same lifetime.
- Rejected uploads are deleted immediately by the request handler.
- Deleted metadata enqueues an idempotent cleanup message keyed by stored file name.
- Cleanup retries are bounded and expose failures through worker health and logs.
- Terminal cleanup failures require an operator review; they must not silently discard
  the queue record.

## Reconciliation

`ProjectTaskAttachmentReconciliationService` compares metadata and provider inventory.
It reports metadata without binaries and binaries without metadata, but never deletes
objects automatically. Operators must investigate a report before scheduling cleanup.

Run reconciliation from an authenticated administrative job or maintenance command in
the target environment. Providers without object enumeration must supply their own
inventory adapter.

## Backup and restore

A valid backup contains one PostgreSQL snapshot and one attachment-storage snapshot
from the same maintenance window.

1. Stop writes or place the application in maintenance mode.
2. Back up PostgreSQL with the platform-supported `pg_dump` or snapshot mechanism.
3. Snapshot or copy the complete attachment storage root while preserving object names.
4. Record application version, migration version, UTC timestamp and checksums.
5. Restore PostgreSQL and attachment objects into an isolated environment.
6. Start the application, run migrations, then run reconciliation.
7. Verify upload, download and delete before promoting the restored environment.

A database-only or binary-only copy is not a complete backup. Restore drills must be
performed periodically in the selected production platform.

## Malware scanning boundary

Production upload acceptance requires an implementation of a malware-scanning port
selected for the deployment platform. Until a scanner is configured, the application
provides content-signature inspection and extension/MIME validation, but must not claim
malware protection.

The upload pipeline invokes `IProjectTaskAttachmentMalwareScanner` before binary or
metadata persistence when `Attachments:RequireMalwareScan` is enabled. Production
configuration is rejected at startup unless this setting is `true`. The registered
fallback scanner returns `Unavailable`, so uploads fail closed until the deployment
replaces it with a provider adapter. Threat results are rejected as invalid content;
timeouts, scanner errors, and unavailable results must never be converted to `Clean`.

Recommended provider choices:

- Azure Blob Storage with Microsoft Defender for Storage scanning;
- S3-compatible object storage with an event-driven scanner;
- ClamAV for self-hosted deployments.

The production adapter must quarantine new objects before they become downloadable,
persist a safe scan status, handle timeout/failure as non-clean, and expose scan backlog
and failure metrics. Scanner credentials and storage keys must remain outside the
repository.

The provider adapter, quarantine lifecycle, persisted scan status, and platform metrics
cannot be certified by this repository until a production storage/scanner provider is
selected. They remain mandatory deployment work rather than capabilities supplied by
the fallback implementation.

## Alerts

Alert on cleanup terminal failures, oldest pending cleanup age, reconciliation drift,
scan failures, scan backlog age and storage health. Logs must contain only generated
storage keys and correlation identifiers, never binary content or signed URLs.