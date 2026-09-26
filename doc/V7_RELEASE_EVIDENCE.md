# V7 Release Evidence

This record captures the repository evidence for the completed V7 MediatR
migration. It complements the reusable checklist in
[`V7_RELEASE_GATE.md`](V7_RELEASE_GATE.md). It is not a replacement for the
runtime, backup, restore and promotion evidence required by the V5 operations
gate.

## Release candidate status

| Item | Value |
| --- | --- |
| Release commit | To be recorded after merge |
| Pull request | To be recorded after creation |
| Local Docker daemon | Unavailable during this validation session |
| CI Docker evidence | Required before tagging `v7.0.0` |

## Migrated slices

| Module | Request/handler pairs | Dispatch path |
| --- | ---: | --- |
| `Notifications` | 6 | `ISender -> IRequestHandler` |
| `Projects` | 17 | `ISender -> IRequestHandler` |
| `ProjectTasks` | 13 | `ISender -> IRequestHandler` |
| **Total** | **36** | One MediatR path per migrated slice |

The migration removes direct handler interfaces and module-level handler
registrations. Focused ports, resource authorization, explicit transactions,
optimistic concurrency, durable notifications, email outbox records,
attachment cleanup and worker registrations remain in their existing owners.

## Local validation

The following checks passed:

- backend Release build: 0 warnings and 0 errors;
- migrated Projects/ProjectTasks/architecture unit filter: 196/196;
- full backend unit suite: 347/347;
- Projects, ProjectTasks and ProjectInvitations API suites: 49/49;
- MediatR/module architecture integration checks: 3/3.

The full integration invocation produced 103 passing tests and 2 skipped tests.
its 26 PostgreSQL/Testcontainers cases could not start because the local Docker
endpoint `npipe://./pipe/docker_engine` was unavailable. These cases are not
reported as passed; CI must provide the runtime evidence before release.

## Contract evidence

- No HTTP route was changed.
- No JSON response type or status-code contract was changed.
- No database migration or schema change was introduced.
- `Domain` remains free of a MediatR reference.
- The architecture tests verify one handler per request, DI resolution and
  `ISender` usage for migrated module controllers.
- The telemetry behavior records request type, duration, outcome and
  correlation context without serializing request payloads or exception
  messages.

## Accepted boundaries

V7 intentionally does not add global transactions, validation or retry
behaviors, cache/Redis, a message broker, microservices or provider-level
email idempotency. Optional identity, product and distributed-operations
directions remain separate ADR candidates. V8 begins only after measuring the
manual cost of creating and maintaining additional slices. V8.0 now provides a
bounded source-template proof based on the already confirmed slice standard; the
historical wall-clock manual measurement remains a V8.1 follow-up.

## Final release record

Complete this section after the pull request and CI run are available:

- merged commit SHA: `TBD`;
- pull request: `TBD`;
- required CI run: `TBD`;
- Docker/Testcontainers result: `TBD`;
- tag: `TBD`.
