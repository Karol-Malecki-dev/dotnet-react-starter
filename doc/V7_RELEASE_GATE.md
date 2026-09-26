# V7 MediatR Release Gate

V7 is accepted only when the completed MediatR migration is reproducible and
the existing module, transaction, authorization and durable-side-effect
contracts remain intact. This gate covers the accepted MediatR track only.
Optional identity, product, distributed-architecture and multi-region ideas
remain separate decisions.

## Release scope

The V7 release scope is:

- one explicit `AddApplicationDispatch` registration and one handler assembly
  scan;
- the `Projects/GetProjectDetails` query pilot;
- the `ProjectTasks/CreateProjectTask` command pilot;
- safe MediatR request telemetry;
- the complete `Notifications` migration;
- the complete `Projects` migration;
- the complete `ProjectTasks` migration;
- removal of the direct-handler dispatch paths for migrated slices;
- unchanged HTTP routes, JSON contracts, status codes and database schema.

MediatR is an in-process dispatcher in this scope. It does not replace domain
invariants, resource authorization, transaction ownership, optimistic
concurrency, durable notifications, the email outbox, workers or future
integration events.

## Repository gate

Run these checks from the repository root on the exact commit intended for
release:

```powershell
dotnet build backend\backend.slnx --configuration Release
dotnet test backend\UnitTests\UnitTests.csproj --configuration Release
dotnet test backend\IntegrationTests\IntegrationTests.csproj --configuration Release
```

The PostgreSQL/Testcontainers integration tests require a running Docker
daemon. If Docker is unavailable locally, the complete integration gate must
be supplied by CI rather than silently treating those tests as passed.

The focused migration checks are useful for fast feedback:

```powershell
dotnet test backend\UnitTests\UnitTests.csproj --configuration Release `
  --filter "FullyQualifiedName~Modules.Projects|FullyQualifiedName~Modules.ProjectTasks|FullyQualifiedName~Architecture"

dotnet test backend\IntegrationTests\IntegrationTests.csproj --configuration Release `
  --filter "FullyQualifiedName~ProjectsApiIntegrationTests|FullyQualifiedName~ProjectTasksApiIntegrationTests|FullyQualifiedName~ProjectInvitationsApiIntegrationTests|FullyQualifiedName~ModuleArchitectureIntegrationTests"
```

## Architecture and behavior gates

The release candidate must demonstrate all of the following:

1. Every `IRequest<TResult>` in the migrated modules has exactly one
   `IRequestHandler<TRequest, TResult>`.
2. Every migrated request handler resolves through DI.
3. Every migrated module controller dispatches through `ISender`; controllers
   do not depend on concrete handlers or `IMediator`.
4. `Domain` has no MediatR dependency.
5. Module controllers and handlers do not depend directly on
   `ApplicationDbContext`.
6. No migrated slice has a second direct-handler dispatch path.
7. Cancellation tokens reach the handler and its persistence/storage calls.
8. Authorization, concurrency conflicts, activity records, durable
   notifications, email-outbox records, attachment cleanup and workers retain
   their existing behavior.
9. Route uniqueness and the existing API integration suites remain green.
10. The Release build has no new warnings.

## CI and merge gate

Before tagging V7:

1. open a pull request from the completed migration branch;
2. require green backend unit, integration, architecture and Docker jobs;
3. require the final release-candidate commit to be the tested PR head;
4. resolve review comments that would change the documented contracts;
5. merge through the repository's required checks.

The local evidence document must be updated with the merged commit, pull
request, CI run and Docker result before creating `v7.0.0`.

## Explicit exclusions

The V7 gate does not require:

- passkeys, OIDC/SSO or a new identity provider;
- multi-tenancy, API keys or public API versioning;
- microservices, a message broker or a second database;
- Redis, a global transaction behavior, a validation behavior or a retry
  behavior;
- a request-latency benchmark. V6 remains the owner of performance baselines,
  and V7 makes no performance claim.

## Final release record

Before tagging, record:

- the merged release commit SHA;
- the pull request number and CI run URL;
- backend build and test totals;
- the Docker/Testcontainers result;
- the accepted exclusions above;
- any follow-up issue for optional V7 directions or V8 platformization.
