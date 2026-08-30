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

The remaining task commands and queries are transitional. They will be extracted
one use case at a time after each slice has tests and a stable dependency boundary.

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

- During migration, old and new service styles coexist.
- Some shared task ports remain under the older `Application.Features` namespace.
- A central `DbContext` means EF configurations and migrations are not physically
  isolated yet.
- The frontend remains organized by its current feature/service structure until the
  backend slice contracts stabilize.

## Validation

The first slice is considered aligned with this ADR when:

- the handler unit tests cover authorization, validation, persistence and
  notification behavior;
- the existing project-task integration tests pass without route changes;
- the backend Release build passes;
- the module registration resolves the handler and existing task services;
- no EF migration is required for the structural change.

## Follow-up

- Extract `GetProjectTaskDetails` or the next smallest read slice.
- Move task-specific ports into the module namespace once no transitional consumer
  depends on the old location.
- Add a module registration test if the composition root grows beyond one focused
  module.
- Revisit separate projects, packages or a `dotnet new` template only after several
  modules are complete and repeated setup cost is measured.
