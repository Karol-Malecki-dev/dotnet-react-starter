# Contributing

Thank you for improving the .NET React Starter. This repository is both an
executable reference architecture and a reusable source-template project, so
changes should preserve behavior, boundaries, documentation, and the proof
that the repository still works for new consumers.

## Before you start

Read the relevant parts of:

1. [README.md](README.md) or [README.PL.md](README.PL.md);
2. [Getting started](doc/GETTING_STARTED.md);
3. [Project architecture](doc/ARCHITECTURE.md);
4. [Adding features](doc/ADDING_FEATURES.md);
5. [CI/CD](doc/CI_CD.md);
6. [Security policy](SECURITY.md).

For backend work, also read
[backend.instructions.md](.github/instructions/backend.instructions.md) and
the repository-wide
[Copilot instructions](.github/copilot-instructions.md).

## Development workflow

1. Start from an up-to-date branch and create a focused branch such as
   `feature/<short-name>`, `fix/<short-name>`, `docs/<short-name>`, or
   `chore/<short-name>`.
2. Define the actor, expected result, invariants, owning layer, failure paths,
   transaction boundary, and cheapest decisive test before editing.
3. Search for the nearest existing pattern and reuse it before introducing a
   new abstraction or package.
4. Make one coherent vertical slice at a time. Keep unrelated worktree changes
   intact and do not rewrite history belonging to another task.
5. Update directly related documentation, configuration examples, migrations,
   generated consumer evidence, and release notes when the change affects
   them.
6. Run the narrowest decisive checks first, then the affected build or broader
   suite when the change crosses a contract, persistence, routing, migration,
   concurrency, or module boundary.

## Architecture rules

The project uses a modular monolith with vertical slices:

- `API` owns HTTP binding, public contracts, validation, authorization,
  middleware, status-code mapping, health endpoints, and composition-root
  registration.
- `Application` owns use cases, commands/queries, results, focused ports, and
  orchestration.
- `Domain` owns entities, value objects, enums, aggregates, and invariants.
- `Infrastructure` owns EF Core, PostgreSQL, migrations, storage, email,
  workers, and external integrations.
- `Shared` contains deliberately shared settings, DTOs, and response helpers.

Keep domain rules in the domain or owning application module. Do not return
HTTP types from handlers or stores, place domain rules in controllers, or reach
into another module's persistence. Cross-module access must use an explicit
port, identifier, or application workflow.

New backend command/query slices should follow the established MediatR path:
`ISender`, `IRequest<TResult>`, and
`IRequestHandler<TRequest, TResult>`. Existing areas may be migrated
incrementally; a repository-wide rewrite is not required.

Frontend changes should preserve the feature-oriented structure, shared API
client, runtime-configuration bootstrap, and backend ownership of
authorization. Use the existing request cancellation, refresh coordination,
error mapping, and feature-flag patterns.

## Local setup

For the complete local environment:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

The stack exposes the frontend on `http://localhost:3000`, the API on
`http://localhost:5000`, Mailpit on `http://localhost:8025`, and MinIO on
`http://localhost:9000`/`http://localhost:9001`. See the
[configuration reference](doc/CONFIGURATION_REFERENCE.md) for overrides.

When running services outside Compose, the backend launch profile uses
`http://localhost:5031` and the Vite development server uses
`http://localhost:3000`.

## Validation commands

Run commands from the repository root.

### Backend

```powershell
dotnet build backend/backend.slnx --configuration Release
dotnet test backend/UnitTests/UnitTests.csproj --configuration Release
dotnet test backend/IntegrationTests/IntegrationTests.csproj --configuration Release
```

Integration tests that use PostgreSQL/Testcontainers require a working Docker
Engine. Do not report those tests as passing when Docker was unavailable.

### Frontend

```powershell
Set-Location frontend
npm ci
npm run test:once
npm run build
Set-Location ..
```

### End-to-end smoke gate

The smoke tests target an already running application. For the complete local
Compose gate, use:

```powershell
.\scripts\Invoke-E2ETests.ps1
```

You can point the tests at another environment with `SMOKE_API_URL` and
`SMOKE_FRONTEND_URL`. Never run smoke tests against an environment or data
without authorization.

## Pull requests

Keep pull requests small enough to review and describe:

- what changed and why;
- the affected layers, contracts, migrations, configuration, or security
  boundaries;
- the tests and validation commands that were run;
- known limitations, follow-up work, and rollback considerations.

Use this checklist before requesting review:

- [ ] The change preserves unrelated worktree changes.
- [ ] Public behavior and failure paths are covered by focused tests.
- [ ] Authorization and validation are enforced at the backend boundary.
- [ ] Database constraints, indexes, and migrations are updated when needed.
- [ ] Configuration examples and documentation are current.
- [ ] No credentials, tokens, personal data, or generated build output were
      committed.
- [ ] CI-relevant commands pass locally or the limitation is explained.
- [ ] V8 template/generator changes include the relevant proof and
      regenerate-and-review impact.

## Commit and documentation conventions

Use short, imperative commit subjects that explain the change. There is no
additional commit-message linter in this repository, so clarity is more
important than forcing an artificial prefix.

Documentation changes should prefer durable, task-oriented pages over adding
large operational sections to the README. Update both README entry points when
the documentation map or public project behavior changes. Keep examples
sanitized and make environment-specific assumptions explicit.

## Generated V8 content

Treat `templates/v8-consumer/` and generated output as a release surface, not
as an unrelated copy of the main application. Read
[templates/v8-consumer/Common/README.md](templates/v8-consumer/Common/README.md)
before changing template files. Follow the
[V8 release gate](doc/V8_RELEASE_GATE.md), run the generators/evidence scripts,
and review the generated diff before claiming the template is updated.

## Security and conduct

Do not disclose vulnerabilities publicly; follow [SECURITY.md](SECURITY.md).
Do not commit secrets or real customer data. Be specific, respectful, and
constructive in issues and reviews. If a proposed change conflicts with a
security, data-integrity, or architectural invariant, document the trade-off
instead of silently weakening the invariant.
