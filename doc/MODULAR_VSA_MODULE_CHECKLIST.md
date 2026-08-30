# Modular VSA Module Checklist

Use this checklist before considering a new module or vertical slice complete.
The checklist is intentionally small enough to use during normal feature work.

## Module boundary

- [ ] The business responsibility and aggregate ownership are stated.
- [ ] Public dependencies on other modules are listed.
- [ ] Cross-module rules use an explicit port, identifier or application workflow.
- [ ] API code does not access `ApplicationDbContext` directly.
- [ ] The module has one composition extension for its registrations.
- [ ] Optional workers and endpoints have an explicit enablement policy.
- [ ] Database tables, foreign keys and migration ownership are documented.

## Vertical slice

- [ ] The use case has a focused command/query.
- [ ] The handler contract is in `Application`.
- [ ] The handler implementation is in `Infrastructure` or the selected adapter
      assembly and depends on ports, not API types.
- [ ] HTTP request and response contracts are explicit.
- [ ] Input validation is present where the use case accepts external input.
- [ ] Authorization is enforced on the server and is covered by a test.
- [ ] Domain invariants are enforced by the entity or aggregate.
- [ ] Persistence changes use focused ports and define the transaction boundary.
- [ ] Errors and concurrency conflicts map to documented status codes.
- [ ] DI registration is made through the module extension.
- [ ] Unit tests cover the handler's success and meaningful failure paths.
- [ ] Integration tests cover the public route and persistence behavior.
- [ ] Frontend types, API client, loading/error states and UI tests are updated when
      the slice is exposed in the UI.
- [ ] Documentation and an ADR are updated when a boundary or contract changes.

## Definition of Done

- [ ] Targeted tests pass.
- [ ] The relevant backend build passes with no new warnings.
- [ ] PostgreSQL tests pass when the change affects transactions, constraints or
      optimistic concurrency.
- [ ] Frontend build/tests pass when frontend code or API contracts change.
- [ ] `git diff --check` passes.
- [ ] No new broad service method was added when a slice-specific handler was
      appropriate.
- [ ] The old path is removed only after all consumers and tests use the new slice.
- [ ] Runtime flags control availability/UX, not server-side authorization.
