# Project Copilot Instructions

These repository-level instructions define the portable engineering baseline for
this project and for consumers generated from the V8 template. They must remain
free of personal data and user-specific context.

## Engineering priorities

- Preserve stable behavior, security, data integrity, and explicit failure paths.
- Inspect the current implementation, configuration, tests, and nearest existing
  pattern before proposing or applying a change.
- Separate facts, assumptions, unknowns, constraints, and decisions.
- Prefer the smallest coherent change that solves the requirement.
- Reuse established project patterns before introducing abstractions, packages,
  tools, or new architectural boundaries.
- Do not hide errors behind broad catches, silent fallbacks, or success-shaped
  responses.
- Preserve unrelated user changes and avoid unrelated refactoring.

## Working method

- Work on one coherent vertical slice or one hardening topic at a time.
- Define the actor, expected result, invariants, rule owner, data owner,
  meaningful failure paths, transaction boundary, and cheapest decisive test.
- Compare only a few realistic implementation options and state the recommended
  smallest variant.
- Separate work required now, reasonable follow-up work, and out-of-scope ideas.
- Implement in small, reversible checkpoints.
- After a change, explain layer responsibilities, risks, validation results, and
  remaining gaps.

## Conversation modes

- `PLAN ONLY`: analyze facts, options, risks, and validation without editing.
- `IMPLEMENT`: make the approved, focused change and validate it.
- `REVIEW`: assess the current code or diff without expanding the scope.
- `DEBUG`: reproduce the symptom, test a hypothesis, make the smallest repair,
  and add or run a regression check.
- `TEACH-BACK`: check understanding of the flow and failure paths without
  replacing the explanation with generated code.

When the requested outcome is ambiguous, clarify the mode and scope before
editing. Keep technical decisions and repository rules independent from
personal preferences.

## Architecture

- Keep domain rules and invariants in the domain model or the owning module.
- Keep application contracts, results, focused ports, and use-case orchestration
  separate from HTTP and persistence details.
- Keep infrastructure responsible for EF Core, PostgreSQL, migrations,
  external integrations, workers, and implementations of application ports.
- Keep API responsible for binding, HTTP contracts, validation, authorization,
  and mapping application results to status codes.
- Do not return HTTP types from handlers or stores.
- Do not place domain rules in controllers.
- Prefer explicit module entry points and composition-root registration.
- Cross-module access must use an explicit port, identifier, or application
  workflow rather than reaching into another module's persistence.
- Rules enforceable by the database should also be protected by PostgreSQL
  constraints or indexes when applicable.

## Validation

- Run the narrowest decisive test first.
- Run the affected build and expand validation when contracts, DI, routing,
  persistence, migrations, concurrency, or module boundaries change.
- Use unit tests for domain rules and isolated handler behavior.
- Use API integration tests for public routes, authorization, and response
  contracts.
- Use PostgreSQL-backed tests for transactions, constraints, migrations, and
  concurrency.
- Use frontend or browser tests when the user-visible workflow changes.
- Do not claim PostgreSQL or Docker validation passed when the environment could
  not run it.

## Documentation and safety

- Document durable architectural decisions and contract changes.
- Keep public DTOs, endpoints, validation, status codes, and example payloads
  understandable.
- Mark generated skeletons, placeholders, and intentional limitations clearly.
- Surface validation failures explicitly and preserve actionable diagnostics.
- Do not perform Git branch, commit, merge, rebase, reset, stash, pull, push, or
  tag operations without a direct request.

Backend-specific rules are in
`.github/instructions/backend.instructions.md`.
