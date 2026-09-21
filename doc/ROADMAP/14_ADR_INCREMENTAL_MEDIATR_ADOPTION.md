# ADR: Incremental MediatR Adoption for Modular VSA

- Status: Accepted for planned implementation
- Date: 2026-09-21
- Scope: in-process command/query dispatch in backend vertical slices

## Context

The backend Vertical Slice Architecture was first implemented with explicit handler
interfaces. This made the request flow, module boundaries, focused persistence ports
and transaction ownership visible while the standard was still being discovered.

`Projects`, `ProjectTasks` and `Notifications` now provide enough command and query
slices to adopt a common dispatcher without using it to invent the architecture.
Learning MediatR is also an explicit project goal because it is commonly encountered
in ASP.NET Core applications that use CQRS-style command/query handlers and pipeline
behaviors.

The adoption must preserve the properties already proven by the explicit-handler
implementation:

- business modules remain the primary boundary;
- public HTTP routes, JSON contracts and status codes remain stable;
- domain entities protect invariants without depending on MediatR;
- handlers use focused ports rather than `ApplicationDbContext`;
- transaction, concurrency and outbox behavior remain explicit and testable;
- durable user notifications and integration events are not replaced by in-process
  publication.

## Decision

Adopt MediatR incrementally as the in-process dispatcher for backend commands and
queries.

The target request flow is:

```text
HTTP adapter
    -> ISender.Send(command/query)
    -> IRequestHandler<TRequest, TResult>
    -> domain rules and focused application ports
    -> infrastructure adapters
```

The following rules apply:

1. Commands and queries live in the existing
   `Application/Modules/<Module>/<UseCase>/` slice and implement
   `IRequest<TResult>`.
2. Handler implementations remain in `Infrastructure` during the pilot and implement
   `IRequestHandler<TRequest, TResult>`. MediatR adoption does not require moving
   domain or persistence code between projects.
3. HTTP adapters depend on `ISender`, not on `IMediator`, because they dispatch
   requests but do not publish in-process notifications.
4. MediatR registration is performed once through an explicit
   `AddApplicationDispatch` extension that scans the known handler assembly once.
   Existing module entry points continue to register their focused ports, adapters,
   workers and options.
5. The `Domain` project must not reference MediatR.
6. Resource authorization remains in the use-case handler or an existing explicit
   authorization port. It is not hidden in a generic pipeline behavior.
7. Transaction ownership and the final `SaveChangesAsync` remain explicit in each
   command. A global transaction behavior is outside the first adoption.
8. Existing API validation remains authoritative during the pilot. A validation
   behavior may be adopted later only through a separate decision that removes
   duplicate validation and preserves the current error contract.
9. The first cross-cutting behavior records request type, duration,
   completed/cancelled/failed outcome and correlation context without logging request
   payloads or secrets.
10. `MediatR.INotification` is not a replacement for durable application
    notifications, the email outbox or future integration events.

## Pilot slices

The first implementation uses two representative slices:

| Slice | Type | Why it is selected |
| --- | --- | --- |
| `Projects/GetProjectDetails` | Query | Small read path with a focused store and stable public contract. |
| `ProjectTasks/CreateProjectTask` | Command | Authorization, domain creation, activity, notification and one final commit. |

Each slice is migrated end to end:

- request implements `IRequest<TResult>`;
- handler implements `IRequestHandler<TRequest, TResult>`;
- controller uses `ISender.Send`;
- direct handler interface is removed after all consumers and tests use MediatR;
- unit and integration tests continue to assert the same behavior;
- no route, response, database schema or frontend contract changes.

The pilot controls the implementation details and migration pace. The decision to
adopt MediatR is accepted; it is not a temporary spike that is discarded without a
recorded superseding ADR.

## Pipeline behavior boundary

The first behavior is limited to safe telemetry:

```text
request type + duration + completed/cancelled/failed outcome + correlation ID
```

It must not:

- serialize command/query payloads;
- log passwords, tokens, codes, email contents or attachment data;
- make resource-authorization decisions;
- call `SaveChangesAsync`;
- retry non-idempotent commands;
- convert every exception into a success-shaped result.

Validation, transactions, retries and caching require separate evidence and are not
added merely because MediatR supports pipeline behaviors.

## Incremental migration order

After both pilot slices and architecture guardrails pass:

1. new backend command/query slices use MediatR by default;
2. `Notifications` migrates first because its read and preference operations are
   comparatively small;
3. `Projects` migrates one use case at a time;
4. `ProjectTasks` migrates last because it contains the most complex transactional,
   attachment and worker-related workflows;
5. `Identity` migrates only when a real auth change touches a specific use case.

There is no repository-wide flag day. One slice must not have two active dispatch
paths after its migration is complete.

## Architecture guardrails

Automated tests must verify:

- every migrated `IRequest<TResult>` has exactly one
  `IRequestHandler<TRequest, TResult>`;
- MediatR and every pilot handler resolve from DI;
- migrated HTTP adapters use `ISender`;
- `Domain` has no MediatR dependency;
- module controllers and handlers still do not depend directly on
  `ApplicationDbContext`;
- route uniqueness and existing module registration tests continue to pass;
- cancellation tokens reach handlers and persistence calls;
- product notifications continue to use their durable database/outbox path.

## Package and supply-chain gate

Before changing dependency manifests:

- select and pin a stable MediatR version compatible with the repository SDK and
  target framework;
- verify its current license and any license-key or runtime-warning requirements;
- review direct and transitive dependencies;
- document the selected package version and registration API in the implementation
  change.

The roadmap does not preselect a package version because this information must be
verified at implementation time.

## Implementation checkpoints

| Checkpoint | Suggested branch | Scope | Exit gate |
| --- | --- | --- | --- |
| 1. Query foundation | `feature/mediatr-query-pilot` | Package/license review, `AddApplicationDispatch`, `GetProjectDetails`, DI and handler-uniqueness guardrails. | Query unit/API tests and backend Release build pass with unchanged response contract. |
| 2. Command and telemetry | `feature/mediatr-command-pilot` | `CreateProjectTask`, safe telemetry behavior, cancellation tests and existing transaction side effects. | Task unit, API and PostgreSQL-relevant tests pass; no payload is logged. |
| 3. New-slice default | `feature/mediatr-vsa-standard` | Update reference docs/templates and make MediatR the default for new backend slices. | Checklist and architecture tests describe one canonical target. |
| 4. Notifications migration | `refactor/mediatr-notifications` | Migrate one notification use case at a time and remove its old direct-handler interface. | Notification unit/integration tests pass after every slice. |
| 5. Projects migration | `refactor/mediatr-projects` | Migrate project lifecycle, membership, invitations, activity and dashboard incrementally. | Project API and PostgreSQL concurrency/transaction tests remain green. |
| 6. ProjectTasks migration | `refactor/mediatr-project-tasks` | Migrate task, comment and attachment slices without changing workers or durable side effects. | Task, attachment, worker and architecture suites remain green. |

`Identity` is intentionally absent from the batch sequence. It migrates use case by
use case when a real authentication or account-security change requires touching it.

Each checkpoint is independently reversible and must finish without leaving two
active dispatch paths for a migrated slice.

## Options rejected

### Keep explicit handlers permanently

This remains technically valid, but it does not satisfy the accepted learning goal
of implementing and operating a representative MediatR-based VSA.

### Rewrite all slices in one branch

This would mix mechanical migration with behavior changes across multiple critical
workflows and make regressions difficult to locate.

### Build a custom mediator

It would add maintenance cost while providing less workplace-relevant experience.

### Use `INotification` for durable side effects

In-process publication does not provide the durability, replay and failure handling
required by user notifications or cross-service integration.

## Consequences

### Positive

- the project exercises a common .NET command/query dispatch model;
- controllers use one stable dispatch contract;
- safe cross-cutting telemetry can be implemented once;
- new slices have less custom handler-interface registration;
- the migration remains compatible with existing module and persistence boundaries.

### Costs

- request flow becomes less explicit at the call site;
- assembly registration and handler resolution need new guardrails;
- Application, API and Infrastructure gain a library dependency;
- two handler styles coexist during migration;
- package licensing and version upgrades become an ongoing maintenance concern.

## Rollback

The pilot changes no database schema or public HTTP contract. If the selected package
or registration model is unacceptable, each pilot slice can return to its explicit
handler interface without data migration. A rollback of the architectural decision
requires a superseding ADR and removal of the package, registrations and pipeline
behaviors.

## Definition of Done

- package, version and license review are recorded;
- both pilot slices use `ISender` and `IRequestHandler`;
- safe request telemetry behavior is covered by tests;
- targeted unit and API integration tests pass without contract changes;
- architecture tests cover handler uniqueness, DI and the Domain dependency rule;
- backend Release build has no new warnings;
- measured costs and benefits are added to this ADR;
- new slices use MediatR after the pilot gate;
- existing modules have an explicit incremental migration order.
