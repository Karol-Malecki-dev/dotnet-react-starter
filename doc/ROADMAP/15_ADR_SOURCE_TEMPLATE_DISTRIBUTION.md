# ADR: V8 source-template distribution

## Status

Accepted for V8.0 on 2026-09-26.

## Context

The starter has stable backend conventions for modular vertical slices and a
single MediatR dispatch boundary. The next risk is not missing abstractions; it
is copying a large, unstable framework into every future project. A first
platformization step must preserve consumer ownership of domain rules,
authorization, persistence and migrations.

The repository also needs a second consumer proof before it can claim that the
starter is reusable rather than merely self-documenting.

## Decision

V8.0 uses a **source-template with thin PowerShell orchestration**:

- `templates/v8-consumer/Common` is the canonical shared source;
- `Variants/Full` contains only the additional files for the full variant;
- `New-V8StarterProject.ps1` copies a selected variant and writes a manifest;
- `New-VsaSlice.ps1` creates a bounded command/query skeleton;
- `Invoke-V8ScaffoldingProof.ps1` validates two independent generated consumers;
- generated consumers use `regenerate-and-review` for updates.

The manifest is part of the consumer contract:

```json
{
  "templateVersion": "8.0.0",
  "distribution": "source-template",
  "updatePolicy": "regenerate-and-review"
}
```

The generated handler is deliberately incomplete. The generator must not infer
business ownership, authorization, persistence ports, transaction boundaries,
database migrations, workers or events.

## Alternatives considered

### `dotnet new` package

This could improve discoverability and parameter validation, but adds packaging,
template-versioning and CI maintenance before the project has measured demand.
It remains a V8.1 option.

### NuGet modules

Packages could simplify code updates, but would freeze public APIs and make
consumer customization harder. The current modules contain domain and database
decisions that are not stable enough for this distribution model.

### Automatic multi-project updater

An updater could reduce repetitive changes, but it would need migration,
conflict-resolution and consumer customization semantics. V8.0 has only two
proof consumers and no evidence that this complexity is justified.

## Consequences

Positive:

- the reusable contract is visible as source and easy to debug;
- consumers can modify generated code without runtime coupling;
- the proof is deterministic and works for both minimal and full variants;
- version and update ownership are explicit;
- no database or deployment abstraction is invented prematurely.

Costs:

- updates require a deliberate regenerate-and-review workflow;
- copied source can diverge between consumers;
- V8.0 does not provide package-level dependency resolution;
- future template changes need compatibility review.

## Update procedure

1. Bump `templateVersion` according to the compatibility impact.
2. Generate fresh `Minimal` and `Full` disposable consumers.
3. Run the V8 proof and inspect manifest and contract changes.
4. Compare the generated output with each consumer.
5. Port changes deliberately, including migrations and frontend contracts where
   the consumer owns them.
6. Run the consumer's own build, tests and release gate.

This procedure is intentionally manual until several real consumers demonstrate
that automation would have a lower total cost.

