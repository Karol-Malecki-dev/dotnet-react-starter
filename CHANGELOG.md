# Changelog

All notable repository changes are recorded here. This project uses annotated
Git tags as release markers; the entries below summarize the current
operations and reusable-starter line without inventing dates that are not
stored in the repository history.

## [Unreleased]

### Documentation

- Modernized the English and Polish README entry points with current ports,
  runtime versions, architecture, security, testing, CI/CD, VPS, and V8
  information.
- Added the security policy, contributor workflow, configuration reference,
  troubleshooting guide, and API reference.
- Added a durable documentation map for onboarding, operations, and release
  review.

### Maintenance

- Replaced the stale `weatherforecast` example in `backend/API/API.http` with
  current health, runtime-configuration, and authenticated API probes.

## [8.0.1] - Portable AI instructions

- Standardized portable AI-assisted development instructions and repository
  guidance.
- Preserved the V8 reusable-starter scope while making the engineering
  workflow portable for generated consumers.

See [V8 release gate](doc/V8_RELEASE_GATE.md) and
[AI-assisted development workflow](doc/AI_ASSISTED_DEVELOPMENT_WORKFLOW.md).

## [8.0.0] - Reusable starter

- Added Minimal and Full source-template consumers.
- Added PowerShell project and vertical-slice generators.
- Added manifest-based `regenerate-and-review` update policy.
- Added end-to-end V8 scaffolding proof and release documentation.
- Synchronized roadmap, architecture decisions, and reusable-consumer
  documentation.

## [7.0.0] - MediatR migration

- Completed the incremental MediatR migration for command/query slices.
- Standardized notification and feature-slice dispatch through the application
  boundary.
- Added command telemetry/query pilot patterns and related documentation.

## [6.0.0] - Performance and reliability

- Added measured API and PostgreSQL query-plan baselines and release evidence.
- Added task pagination indexing, notification idempotency, outbox leasing,
  dead-letter state, and queue metrics.
- Added frontend request cancellation, single-flight refresh coordination,
  offline retry recovery, and bounded email-delivery operations.
- Isolated local Compose environments and documented the performance/reliability
  release gate.

See [V6 release gate](doc/V6_RELEASE_GATE.md).

## [5.1.0] - Staging validation

- Added repeatable staging verification and release evidence capture.
- Added off-host backup checks and Compose pull/build retry behavior for the
  staging release gate.

## [5.0.0] - Production operations

- Added protected VPS staging and production deployment topology.
- Added controlled production migrations, encrypted backup/restore procedures,
  monitoring, alert routing, and operational runbooks.
- Added scanned container delivery, immutable image promotion, and deployment
  rollback evidence.

See [VPS deployment runbook](doc/V5_DEPLOYMENT_RUNBOOK.md) and
[CI/CD](doc/CI_CD.md).

## Release policy

- Release tags are immutable historical markers.
- New application, infrastructure, security, or template behavior should be
  added under `Unreleased` first and moved to a versioned section when tagged.
- A release should link its CI/CD evidence, migration/rollback notes, security
  impact, and relevant release gate.
- Documentation-only corrections may remain in `Unreleased` until the next
  tagged release.

[Unreleased]: https://github.com/Karol-Malecki-dev/dotnet-react-starter/compare/v8.0.1...HEAD
[8.0.1]: https://github.com/Karol-Malecki-dev/dotnet-react-starter/releases/tag/v8.0.1
[8.0.0]: https://github.com/Karol-Malecki-dev/dotnet-react-starter/releases/tag/v8.0.0
[7.0.0]: https://github.com/Karol-Malecki-dev/dotnet-react-starter/releases/tag/v7.0.0
[6.0.0]: https://github.com/Karol-Malecki-dev/dotnet-react-starter/releases/tag/v6.0.0
[5.1.0]: https://github.com/Karol-Malecki-dev/dotnet-react-starter/releases/tag/v5.1.0
[5.0.0]: https://github.com/Karol-Malecki-dev/dotnet-react-starter/releases/tag/v5.0.0
