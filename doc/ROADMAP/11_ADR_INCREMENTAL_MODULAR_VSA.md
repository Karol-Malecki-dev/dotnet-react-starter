# ADR: Incremental Modular Vertical Slices

- Status: Accepted
- Date: 2026-08-30
- Scope: V3 migration toward a reusable modular starter

## Context

The repository already has a layered modular monolith with shared API hosting,
one `ApplicationDbContext`, one PostgreSQL database and several feature-specific
ports. A full rewrite into isolated .NET projects or independent databases would
increase migration risk before the module boundaries are proven.

The `ProjectTask` aggregate has an independent lifecycle, focused persistence ports,
authorization rules and integration coverage. It is therefore a suitable first
module for testing a vertical-slice structure without changing public behavior.

## Decision

Use a hybrid modular monolith:

- business modules contain vertical slices;
- each slice owns its application input, handler contract, HTTP adapter and
  slice-specific validation;
- domain entities remain in the shared domain project until project boundaries
  justify a separate assembly;
- the existing `ApplicationDbContext`, PostgreSQL database and migration assembly
  remain central;
- the composition root calls one registration extension per module;
- existing routes, JSON contracts and status-code behavior are preserved during
  migration;
- controllers remain valid transport adapters, but a new use case must not be
  added to a broad service when a focused handler can own it;
- cross-module rules use explicit application ports or identifiers; direct
  `DbContext` access from API code is not allowed.

The first implementation is the `CreateProjectTask` slice:

```text
API/Modules/ProjectTasks/CreateProjectTask
Application/Modules/ProjectTasks/CreateProjectTask
Infrastructure/Modules/ProjectTasks/CreateProjectTask
```

The initial pilot has since been expanded one use case at a time. The current
`ProjectTasks` backend includes slice-specific handlers for task CRUD, comments,
attachments, deadline reminders and a public dashboard read port. `Projects` now
uses the same structure for lifecycle, membership, invitations, activity and
dashboard use cases. The former broad project controllers, application services and
stores have been removed without changing public routes or JSON contracts.

Cross-module collaboration is explicit:

- member removal calls a write port owned by `ProjectTasks` to stage unassignment;
- the dashboard calls a read port owned by `ProjectTasks` for task metrics and due
  date lists;
- both ports exchange identifiers and read models rather than domain entities;
- all staged relational changes share one scoped `ApplicationDbContext` and one
  final `SaveChangesAsync`.

Database constraints remain part of a slice when they protect its business
invariant. `CreateProjectInvitation` uses a PostgreSQL partial unique index over
`(ProjectId, InvitedUserId, Status)` filtered to `Status = 'Pending'`. Expired
pending rows are transitioned to `Expired` before replacement, while concurrent
inserts are mapped to the existing `409 Conflict` response. Accepted, declined and
expired invitation history is therefore not restricted.

## Options rejected for now

### Full layered-only structure

It is familiar, but broad services and central registration make it easy to omit
validation, tests or a handler boundary when adding a feature.

### Immediate full VSA rewrite

It would touch every controller, contract, service, test and documentation path at
once. The migration could not distinguish architectural improvements from accidental
behavior changes.

### Separate .NET project or database per module

This would add assembly, migration, deployment and transaction complexity without a
current operational requirement. The modular monolith remains the simpler and more
educational default.

### MediatR or a generic message bus

The first slice needs a focused handler contract, not an additional dispatch
abstraction. A broker or mediator can be evaluated only when a real integration or
cross-process requirement exists.

## Consequences

### Positive

- A new use case has a visible, repeatable place for its contract, validation,
  handler, endpoint, persistence adapter, registration and tests.
- Existing consumers are not forced to migrate all routes at once.
- The module boundary can be tested through dependency wiring and API integration
  tests.
- One database and transaction model are retained while the code structure improves.

### Costs and limitations

- Other business areas still use the older service style during migration.
- Some shared task ports remain under the older `Application.Features` namespace.
- A central `DbContext` means EF configurations and migrations are not physically
  isolated yet.
- The frontend remains organized by its current feature/service structure until the
  backend slice contracts stabilize.

## Validation

The current backend modules are considered aligned with this ADR when:

- the handler unit tests cover authorization, validation, persistence and
  notification behavior;
- the existing project-task integration tests pass without route changes;
- the backend Release build passes;
- module registration resolves every Projects and ProjectTasks handler;
- no two actions expose the same HTTP method and attribute route;
- module controllers and handlers do not depend directly on `ApplicationDbContext`;
- folder-only structural moves do not require an EF migration; migrations are
  required when a slice adds or strengthens a database invariant;
- PostgreSQL provider tests apply every migration and cover transaction,
  concurrency and constraint behavior.

The `Notifications` module now follows the same incremental pattern. Its list,
unread-count, email-preference, mark-as-read and mark-all-as-read use cases use
focused handler and persistence contracts registered through
`AddNotificationsModule`. Cross-module notification creation remains an explicit
`INotificationWriter` port for Auth and deadline reminders, while project and task
workflows keep their transaction-owned notification writers.

## Follow-up

- Apply the proven pattern to `Notifications` and later `Identity`, preserving the
  one-use-case-at-a-time boundary.
- Move task-specific ports into the module namespace once no transitional consumer
  depends on the old location.
- Keep the module registration, route uniqueness and dependency guardrails in CI.
- Migrate the frontend to feature-first organization only after the backend
  contracts remain stable.
- Revisit separate projects, packages or a `dotnet new` template only after several
  modules are complete and repeated setup cost is measured.
