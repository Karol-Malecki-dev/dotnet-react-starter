# Attachment hardening plan

## Goal

Task attachments must remain private, bounded and recoverable. A client-provided file
name, content type or size is metadata only and cannot be the security boundary.

The implementation remains incremental. Local storage is suitable for development,
while production storage and malware scanning are adapters selected for a concrete
deployment environment rather than dependencies added in advance.

## Current state

Implemented:

- authenticated upload, list, download and delete vertical slices;
- project membership and role checks for every attachment operation;
- 10 MiB file-size policy and an allowlist of extensions and media types;
- byte-level signatures for PDF, PNG and JPEG;
- required package entries for DOCX and XLSX;
- strict UTF-8 and control-character checks for TXT;
- comparison of declared and actual stream length;
- application-generated storage keys and rejection of traversal, arbitrary names and
  alternate data stream syntax;
- cleanup after metadata persistence failure and a durable cleanup queue for deletes;
- focused unit and API integration tests for the rules above.

Remaining production gaps:

- configurable attachment count and total-byte quotas;
- atomic quota enforcement for concurrent uploads;
- durable production object storage, backup and restore procedures;
- quarantine and malware scanning where required by the deployment threat model;
- retention policy, observability and operational alerts;
- browser-level upload, download and authorization coverage.

## Delivery stages

### Stage 1: Content and path validation

Status: **implemented**.

Keep format inspection in the use-case path so alternate transports cannot bypass it.
The HTTP request-size limit remains a coarse transport guard; the handler owns the
authoritative attachment policy.

### Stage 2: Configurable and atomic quotas

Status: **next**.

Introduce validated attachment options with conservative defaults:

- maximum file size: 10 MiB;
- maximum attachment count per task: 20;
- maximum total attachment bytes per task: 100 MiB.

Enforce count and byte quotas in the database transaction that creates attachment
metadata. Concurrent uploads for the same task must serialize or use a concurrency
token so two requests cannot both pass a stale count. Return a stable validation or
conflict response and do not leave a binary behind when quota reservation fails.

Project-wide or per-user quotas should be added only after product requirements define
ownership, reset periods and administrator behavior. Rate limiting belongs at the HTTP
boundary and complements, but does not replace, persistent quotas.

### Stage 3: Production storage provider

Status: **planned**.

Retain `IProjectTaskAttachmentStorage` as the application port. Make the local root
configurable and fail application startup when local storage is selected in a
production environment without an explicitly mounted persistent path.

Add an object-storage adapter only after the deployment target is selected. It must
use private objects, server-side encryption, bounded retries, cancellation, health
checks and metrics. Downloads continue through the authorized API or use short-lived
signed URLs issued only after the same access check.

The deployment runbook must include backup, restore, orphan reconciliation and a
migration procedure from local files to object storage.

### Stage 4: Quarantine, scanning and retention

Status: **planned when required by the environment**.

Model an explicit attachment lifecycle such as `PendingScan`, `Clean`, `Rejected` and
`Deleted`. New binaries enter quarantine and cannot be downloaded until a scanner
marks them clean. Scanner failures retry without making the file public.

Define retention separately for active attachments, rejected uploads and cleanup
messages. Scheduled cleanup must be idempotent and observable. Logs and audit events
must contain identifiers and outcomes, never file contents or signed download URLs.

### Stage 5: Verification and operations

Status: **continuous**.

Required coverage includes:

- mismatched extension, media type, signature and actual length;
- malformed and misleading Open XML archives;
- path traversal and invalid storage keys;
- exact file, count and total-byte boundaries;
- concurrent uploads near quota;
- owner, member, viewer and outsider authorization;
- storage timeout, metadata failure, retry and orphan cleanup;
- restart persistence, backup restore and browser-level critical paths.

## Definition of done

Attachment hardening is production-ready only when limits are atomic, the selected
production storage survives application replacement, retention and recovery are
documented and tested, and no attachment can be downloaded before all required access
and scanning decisions complete.