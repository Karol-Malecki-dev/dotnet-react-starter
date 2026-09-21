# Backend Development Roadmap

## Purpose

This file is the backend-visible index and backup for the project's development roadmap. The canonical, detailed documents live under [`../doc/ROADMAP/`](../doc/ROADMAP/00_ROADMAP_OVERVIEW.md).

The project is a .NET 9 modular monolith with a React frontend. Backend correctness, security, testability and operational understanding are the primary learning goals.

## Current priority

**V4 notification-contract closure followed by incremental MediatR adoption** is the
current implementation priority.

V1 is complete as the junior baseline and V2 is complete for the current
security-hardening scope. The backend VSA pilot is proven across `Projects`,
`ProjectTasks` and `Notifications`; the next value is a simpler golden path and
end-to-end contract completeness. MediatR is then introduced through one query, one
command and a safe telemetry behavior before becoming the default for new slices.
This is an incremental dispatch migration, not another broad folder rewrite. V5
runtime evidence proceeds independently and still gates any production-ready claim.

## Current progress

As of **2026-09-21**. Percentages follow the calculation documented in the canonical [roadmap overview](../doc/ROADMAP/00_ROADMAP_OVERVIEW.md).

| Stage | Progress | Status |
|---|---:|---|
| V1 | 100% | Complete baseline. |
| V2 | 96% | Complete for the current scope; minor follow-ups remain. |
| V3 | 65% | Domain and transaction work remains, while the backend VSA pilot is complete across three modules with explicit ports, module registration and architecture guardrails. |
| V4 | 78% | Security audit, authorized workspace search, attachment lifecycle and the browser E2E baseline exist; notification contract completeness and remaining evidence are next. |
| V5 | 80% | VPS deployment, controlled migrations, encrypted backup/restore, rollback, monitoring and protected staging smoke are implemented; real staging evidence is still pending. |
| V6 | 13% | Initial foundations; measurement work not started. |
| V7 | 0% | Incremental MediatR adoption is accepted and planned; code implementation has not started. |

**Overall roadmap progress: 62%**.

The current execution order is documented in
[`../doc/PRODUCT_EVOLUTION/DEVELOPMENT_PLAN.md`](../doc/PRODUCT_EVOLUTION/DEVELOPMENT_PLAN.md).

## Stage index

| Stage | Focus | Detailed document |
|---|---|---|
| V1 | Junior baseline and current capabilities | [01_V1_JUNIOR_BASELINE.md](../doc/ROADMAP/01_V1_JUNIOR_BASELINE.md) |
| V2 | Session policy, auth hardening, lockout and API consistency | [02_V2_STABILIZATION_AND_SECURITY.md](../doc/ROADMAP/02_V2_STABILIZATION_AND_SECURITY.md) |
| V3 | Domain boundaries, transactions and optimistic concurrency | [03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md](../doc/ROADMAP/03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md) |
| V4 | Security audit, workspace search, attachment hardening and browser E2E | [04_V4_PRODUCT_COMPLETENESS.md](../doc/ROADMAP/04_V4_PRODUCT_COMPLETENESS.md) |
| V5 | Deployment, secrets, migrations, backups and operations | [05_V5_DEPLOYMENT_AND_OPERATIONS.md](../doc/ROADMAP/05_V5_DEPLOYMENT_AND_OPERATIONS.md) |
| V6 | Measurement, database performance, idempotency and worker reliability | [06_V6_PERFORMANCE_AND_RELIABILITY.md](../doc/ROADMAP/06_V6_PERFORMANCE_AND_RELIABILITY.md) |
| V7 | Planned MediatR adoption plus optional evolution driven by real constraints | [07_V7_OPTIONAL_EVOLUTION.md](../doc/ROADMAP/07_V7_OPTIONAL_EVOLUTION.md) |
| MediatR ADR | Accepted dispatch target and incremental migration | [14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md](../doc/ROADMAP/14_ADR_INCREMENTAL_MEDIATR_ADOPTION.md) |
| Learning workflow | How to work through each stage | [08_LEARNING_WORKFLOW.md](../doc/ROADMAP/08_LEARNING_WORKFLOW.md) |
| Modular VSA checklist | Definition of Done for modules and slices | [MODULAR_VSA_MODULE_CHECKLIST.md](../doc/MODULAR_VSA_MODULE_CHECKLIST.md) |

The overall map is [00_ROADMAP_OVERVIEW.md](../doc/ROADMAP/00_ROADMAP_OVERVIEW.md).

## Execution rules

- Implement one coherent feature or hardening topic per branch.
- Keep the modular monolith unless measurements or operational constraints justify a different boundary.
- Add tests and documentation with each backend change.
- Prefer the smallest change that protects a real invariant or solves a measured problem.
- Do not add technologies only for a CV checklist.
- Treat frontend changes as support for backend workflows unless the task explicitly targets frontend learning.
- Validate the relevant build and tests before considering a stage item complete.

## Recommended branch names

For the documentation work:

```text
docs/project-development-roadmap
```

For the next product increments, use focused branches such as:

```text
feature/v4-notification-contract
```

Other examples are `feature/project-permission-matrix`,
`feature/mediatr-query-pilot`, `feature/mediatr-command-pilot`,
`feature/realtime-notification-delivery`, `feature/task-review-transition`,
`chore/v5-runtime-evidence` and `perf/project-dashboard-query`.

## Learning model

The assistant writes starter implementation, tests and documentation while explaining the reasoning. The project owner should understand the changed code and gradually move to writing approximately 70-80% of the implementation in future projects, using the assistant for planning, review, debugging and verification.

Mid-level material is optional enrichment distributed mainly through V3, V5 and V6. It is not a list of technologies that must be installed before the fundamentals are understood.
