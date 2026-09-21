# Modular VSA Module Checklist

Use this checklist before considering a new module or vertical slice complete.
The checklist is intentionally small enough to use during normal feature work.

Start with the feature proposal when ownership, authorization, state transitions or
failure behavior are not already obvious. Mark a conditional item as `N/A` with a
short reason instead of creating an empty abstraction, event, worker or migration.

## Start gate

- [ ] One actor, one observable outcome and the cheapest discriminating test are stated.
- [ ] The owning business module is named.
- [ ] The use case is classified as a command or query.
- [ ] The closest existing reference slice has been selected.
- [ ] Scope and explicit non-goals fit one coherent change.

## Module boundary

- [ ] The business responsibility and aggregate ownership are stated.
- [ ] Public dependencies on other modules are listed.
- [ ] Cross-module rules use an explicit port, identifier or application workflow.
- [ ] A module does not query another module's `DbSet` directly.
- [ ] API code does not access `ApplicationDbContext` directly.
- [ ] The module has one composition extension for its registrations.
- [ ] Optional workers and endpoints have an explicit enablement policy.
- [ ] Database tables, foreign keys and migration ownership are documented.

## Vertical slice

- [ ] The use case has a focused command/query.
- [ ] The request/result contract is in `Application`; a transitional direct-handler
      interface also remains there until that slice migrates.
- [ ] The handler implementation is in `Infrastructure` or the selected adapter
      assembly and depends on ports, not API types.
- [ ] HTTP request and response contracts are explicit.
- [ ] Input validation is present where the use case accepts external input.
- [ ] Authorization is enforced on the server and is covered by a test.
- [ ] Domain invariants are enforced by the entity or aggregate.
- [ ] Persistence changes use focused ports and define the transaction boundary.
- [ ] Database changes, activity, notifications and outbox records that must be
      atomic are staged before one final `SaveChangesAsync`.
- [ ] Errors and concurrency conflicts map to documented status codes.
- [ ] DI registration is made through the module extension.
- [ ] Unit tests cover the handler's success and meaningful failure paths.
- [ ] Integration tests cover the public route and persistence behavior.
- [ ] Frontend types, API client, loading/error states and UI tests are updated when
      the slice is exposed in the UI.
- [ ] Documentation and an ADR are updated when a boundary or contract changes.

## MediatR-migrated slice

Apply this section after the MediatR pilot gate:

- [ ] The command/query implements `IRequest<TResult>`.
- [ ] Exactly one `IRequestHandler<TRequest, TResult>` is registered.
- [ ] The HTTP adapter dispatches through `ISender`.
- [ ] Cancellation reaches the handler and its I/O calls.
- [ ] The slice has no remaining parallel direct-handler dispatch path.
- [ ] Resource authorization and transaction ownership remain explicit.
- [ ] Pipeline behaviors do not log payloads or secrets.
- [ ] `MediatR.INotification` is not used instead of durable notifications, outbox
      records or integration events.
- [ ] The `Domain` project does not reference MediatR.

## Definition of Done

- [ ] Targeted tests pass.
- [ ] The relevant backend build passes with no new warnings.
- [ ] PostgreSQL tests pass when the change affects transactions, constraints or
      optimistic concurrency.
- [ ] Frontend build/tests pass when frontend code or API contracts change.
- [ ] `git diff --check` passes.
- [ ] Module DI, route uniqueness and direct-`DbContext` architecture guardrails pass.
- [ ] MediatR request/handler uniqueness and DI guardrails pass for migrated slices.
- [ ] No new broad service method was added when a slice-specific handler was
      appropriate.
- [ ] The old path is removed only after all consumers and tests use the new slice.
- [ ] Runtime flags control availability/UX, not server-side authorization.

The implementation order and current command/query references are documented in
[`ADDING_FEATURES.md`](ADDING_FEATURES.md). Larger changes should first use
[`PRODUCT_EVOLUTION/FEATURE_PROPOSAL_TEMPLATE.md`](PRODUCT_EVOLUTION/FEATURE_PROPOSAL_TEMPLATE.md).
MediatR migration rules are recorded in
[`ROADMAP/14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md`](ROADMAP/14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md).
