# V8 Reusable Starter Release Gate

This gate defines the accepted **V8.0 source-template and scaffolding release**.
It does not turn the repository into a general-purpose framework, package
registry, or runtime module installer.

## Release scope

V8.0 includes:

- `templates/v8-consumer/Common` as the canonical source-template;
- `Minimal` and `Full` project variants;
- the `New-V8StarterProject.ps1` project generator;
- the `New-VsaSlice.ps1` query/command skeleton generator;
- explicit project and slice manifests;
- architecture tests for the generated composition root;
- the reproducible proof harness in `Invoke-V8ScaffoldingProof.ps1`.

The source-template is distributed by copy. Its update policy is
`regenerate-and-review`; generated consumers own their copied source and are
not silently changed by a new starter release.

## Required proof

Run from the repository root with a new output directory:

```powershell
.\scripts\Invoke-V8ScaffoldingProof.ps1 `
  -OutputDirectory .\artifacts\v8\release-proof
```

The proof must:

1. generate independent `minimal` and `full` consumers;
2. add both a `Generated/ScaffoldedHealth` query and a
   `Generated/CreateScaffoldedHealth` command through `New-VsaSlice.ps1`;
3. validate the project and slice manifests;
4. verify that source-only `Variants` directories do not leak into output;
5. run `dotnet restore` and a `-warnaserror` zero-warning Release build for
   each backend;
6. run the generated unit and API integration tests;
7. run `npm install` and the frontend production build for each consumer;
8. write a `proof.json` record containing variant counts, elapsed time and
   validation commands.

The proof excludes `bin`, `obj`, `TestResults`, `node_modules` and `dist` from
the recorded source file count. The output directory is ignored by Git; the
release evidence document is the durable summary.

## Architecture gates

The generated project must demonstrate:

1. one explicit MediatR composition root;
2. exactly one handler for every application request;
3. DI resolution for every discovered handler;
4. controller dispatch through `ISender`;
5. no MediatR reference from `Domain`;
6. a working HTTP endpoint reaching the registered handler;
7. no direct persistence dependency in the generated controller or handler;
8. no second dispatch path created by the generator.

These checks are implemented by the generated `UnitTests` architecture tests and
the API integration tests. The generator deliberately does not decide
authorization, domain invariants, persistence ports, transactions, error
mapping, migrations or frontend business behavior.

## Safety gates

- A non-empty destination is rejected unless `-Force` is explicitly supplied.
- Existing generated slice files are not overwritten unless `-Force` is
  explicitly supplied.
- Template build artifacts are excluded from source copying.
- The generated handler is marked as a skeleton and throws
  `NotImplementedException` until the consumer implements its use case.
- A generated manifest must identify the template version, selected modules and
  `regenerate-and-review` policy, including one durable manifest per generated
  slice.

## Database and module boundaries

Database migration validation is `N/A` for V8.0 because the consumer template
contains no `DbContext`, schema or migration. A consuming project owns its
database model and must test empty-database migration and upgrade separately.

The `Full` variant demonstrates source composition with a small `Catalog`
reference module. It is not a claim that production `Projects`,
`ProjectTasks` or `Notifications` can be copied without reviewing their
domain, authorization, persistence and operational contracts.

## Non-blocking V8.1 follow-ups

- measure wall-clock manual creation of several real slices;
- evaluate `dotnet new` only if source-copy ergonomics become insufficient;
- evaluate package distribution only after a module has a stable public API;
- add migration and upgrade proof when the template owns a real schema;
- automate update assistance only if two or more consumers need it.

These items are intentionally not hidden inside the V8.0 release claim.

## Final release record

Before tagging, record:

- the merged release commit SHA;
- the pull request and required CI run;
- the exact `proof.json` result;
- backend and frontend totals;
- accepted V8.1 follow-ups;
- the final tag.
