---
description: "Use when working on ASP.NET, C#, EF Core, PostgreSQL, API, Clean Architecture, Vertical Slice Architecture, testing, migrations, authorization, or concurrency code under backend."
name: "Backend AI Workflow"
applyTo: "backend/**/*"
---

# Backend AI Workflow

These rules complement the project-wide instructions. They describe the
backend-specific workflow and layer boundaries without depending on a particular
person, model, or personal context.

## Working method

- Work on one coherent vertical slice or one hardening topic at a time.
- Inspect the current implementation, configuration, tests, and nearest matching
  pattern first.
- Begin in `PLAN ONLY` mode for planning or review requests; implement only when
  implementation is requested or approved.
- Separate facts, assumptions, unknowns, constraints, and decisions.
- Before editing, state the actor, result, invariants, rule owner, data owner,
  error statuses, atomic write scope, and cheapest test that could disprove the
  hypothesis.
- Compare at most a few realistic options and recommend the smallest coherent
  variant.
- Preserve user changes and do not perform Git operations without an explicit
  request.

## Layer responsibilities

- `Domain` owns entity, aggregate, value-object, and domain invariants. It does
  not depend on HTTP, EF Core, PostgreSQL, or MediatR.
- `Application` owns commands, queries, results, focused ports, and use-case
  orchestration. New and migrated slices use the canonical
  `IRequest<TResult>` and `IRequestHandler<TRequest, TResult>` contracts.
- `Infrastructure` owns EF Core, PostgreSQL, migrations, external integrations,
  workers, persistence adapters, and handler implementations where the current
  modular layout places them.
- `API` owns binding, HTTP request/response models, input validation,
  authorization, endpoint behavior, and result-to-status mapping.
- Handlers and stores do not return HTTP types or status codes.
- Controllers do not own domain rules or persistence decisions.
- API code should not access `ApplicationDbContext` directly when a focused port
  is sufficient.
- Cross-module access goes through an explicit port, identifier, or application
  workflow.
- Rules enforceable by the database should also be protected with a PostgreSQL
  constraint or index.

## Vertical-slice order

Use this as the default order and skip steps that are genuinely not applicable:

1. Define the domain rule and its unit test when the slice changes domain state.
2. Add EF mapping, constraints, and a migration when persistence changes.
3. Add the application command/query, result, focused port, and handler contract.
4. Add the infrastructure store and handler implementation.
5. Add the API request, response, validation, authorization, and controller when
   the use case is public.
6. Add API or PostgreSQL tests, documentation, and broader validation.
7. Add frontend types, client code, state, and UI only after the API contract is
   stable.

Do not create empty repositories, events, workers, migrations, or screens merely
to make folders look symmetrical.

## Validation and learning

- Domain or handler changes require focused unit tests and the affected build.
- Public API or authorization changes require an integration test.
- Constraints, transactions, migrations, or concurrency require a PostgreSQL
  test.
- Testcontainers-based PostgreSQL tests require a working Docker environment.
- Never claim PostgreSQL validation passed based only on unit tests.
- After implementation, explain the data flow, failure paths, risks, validation
  results, and remaining gaps.
- When learning support is part of the request, finish with a short `TEACH-BACK`
  that checks whether the flow and failure paths are understood.

## Conversation modes

- `PLAN ONLY`: analyze the current backend and propose a bounded plan without
  editing files.
- `IMPLEMENT`: make a small approved implementation and validate it.
- `REVIEW`: report defects, risks, and missing tests without automatic edits.
- `DEBUG`: reproduce the failure, test the most likely hypothesis, and make the
  smallest regression-safe repair.
- `TEACH-BACK`: ask the developer to explain the use case, ownership, commit
  point, and failure paths before revealing the answer.
