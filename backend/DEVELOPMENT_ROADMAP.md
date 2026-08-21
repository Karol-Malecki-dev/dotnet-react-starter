# Backend Development Roadmap

## Purpose

This file is the backend-visible index and backup for the project's development roadmap. The canonical, detailed documents live under [`../doc/ROADMAP/`](../doc/ROADMAP/00_ROADMAP_OVERVIEW.md).

The project is a .NET 9 modular monolith with a React frontend. Backend correctness, security, testability and operational understanding are the primary learning goals.

## Current priority

**V2: Stabilization and security** is the next implementation stage.

V1 is mostly achieved. The application already contains authentication, projects, tasks, membership, invitations, comments, attachments, activity, notifications, workers, health checks, Docker wiring and automated tests. The next value comes from hardening existing behavior under replay, concurrency, account changes, restart and partial failure.

## Stage index

| Stage | Focus | Detailed document |
|---|---|---|
| V1 | Junior baseline and current capabilities | [01_V1_JUNIOR_BASELINE.md](../doc/ROADMAP/01_V1_JUNIOR_BASELINE.md) |
| V2 | Session policy, auth hardening, lockout and API consistency | [02_V2_STABILIZATION_AND_SECURITY.md](../doc/ROADMAP/02_V2_STABILIZATION_AND_SECURITY.md) |
| V3 | Domain boundaries, transactions and optimistic concurrency | [03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md](../doc/ROADMAP/03_V3_DOMAIN_TRANSACTIONS_AND_CONCURRENCY.md) |
| V4 | Security audit, workspace search, attachment hardening and browser E2E | [04_V4_PRODUCT_COMPLETENESS.md](../doc/ROADMAP/04_V4_PRODUCT_COMPLETENESS.md) |
| V5 | Deployment, secrets, migrations, backups and operations | [05_V5_DEPLOYMENT_AND_OPERATIONS.md](../doc/ROADMAP/05_V5_DEPLOYMENT_AND_OPERATIONS.md) |
| V6 | Measurement, database performance, idempotency and worker reliability | [06_V6_PERFORMANCE_AND_RELIABILITY.md](../doc/ROADMAP/06_V6_PERFORMANCE_AND_RELIABILITY.md) |
| V7 | Optional evolution driven by real constraints | [07_V7_OPTIONAL_EVOLUTION.md](../doc/ROADMAP/07_V7_OPTIONAL_EVOLUTION.md) |
| Learning workflow | How to work through each stage | [08_LEARNING_WORKFLOW.md](../doc/ROADMAP/08_LEARNING_WORKFLOW.md) |

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

For the immediate V2 work, start with a focused branch such as:

```text
feature/auth-session-hardening
```

Other examples are `feature/optimistic-concurrency`, `feature/security-audit`, `feature/workspace-search`, `chore/deployment-readiness` and `perf/project-dashboard-query`.

## Learning model

The assistant writes starter implementation, tests and documentation while explaining the reasoning. The project owner should understand the changed code and gradually move to writing approximately 70-80% of the implementation in future projects, using the assistant for planning, review, debugging and verification.

Mid-level material is optional enrichment distributed mainly through V3, V5 and V6. It is not a list of technologies that must be installed before the fundamentals are understood.
