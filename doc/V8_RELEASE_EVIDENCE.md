# V8 Release Evidence

This record captures the V8.0 source-template and scaffolding proof. It
complements [`V8_RELEASE_GATE.md`](V8_RELEASE_GATE.md) and does not replace the
V5 runtime, backup, restore or promotion evidence.

## Release candidate status

| Item | Value |
| --- | --- |
| Validation date | 2026-09-26 |
| Release commit | To be recorded after merge |
| Pull request | To be recorded after creation |
| Proof output | `artifacts\v8\generated-run\proof.json` (ignored by Git) |
| Template version | `8.0.0` |
| Distribution | `source-template` |
| Update policy | `regenerate-and-review` |

## Implemented surfaces

| Surface | Evidence |
| --- | --- |
| Project template | `templates\v8-consumer\Common` |
| Minimal variant | Core layered solution with `StarterHealth` |
| Full variant | Minimal variant plus `Catalog` reference module |
| Slice generator | `scripts\New-VsaSlice.ps1` |
| Project generator | `scripts\New-V8StarterProject.ps1` |
| Proof harness | `scripts\Invoke-V8ScaffoldingProof.ps1` |
| Generated manifests | `.starter\manifest.json`, `.starter\slice-manifest.json` and `.starter\slices\*.json` |

## Proof result

The proof was run with:

```powershell
.\scripts\Invoke-V8ScaffoldingProof.ps1 `
  -OutputDirectory .\artifacts\v8\generated-run
```

The final `proof.json` recorded:

| Consumer | Source files | Elapsed time | Unit tests | Integration tests | Frontend |
| --- | ---: | ---: | ---: | ---: | --- |
| `minimal` | 40 | 25.55 s | 7 passed | 3 passed | TypeScript check and Vite build passed |
| `full` | 45 | 23.83 s | 8 passed | 4 passed | TypeScript check and Vite build passed |

Both variants also passed:

- `dotnet restore`;
- `dotnet build backend.slnx --configuration Release --no-restore -warnaserror`;
- manifest validation;
- source-variant leakage validation;
- handler uniqueness and composition-root architecture tests;
- `ISender` controller boundary checks;
- public API endpoint tests.

The recorded source-file count excludes `bin`, `obj`, `TestResults`,
`node_modules` and `dist`. The elapsed time includes dependency restore/install
and is an engineering proof measure, not a production performance claim.

## Contract decisions

- The generator emits `IRequest<TResult>`,
  `IRequestHandler<TRequest, TResult>` and `ISender`.
- MediatR registration remains explicit in one composition-root extension.
- The generated handler is a marked skeleton, not a production use case.
- The consumer owns domain rules, authorization, focused ports, transactions,
  persistence, error mapping and migrations.
- Runtime feature flags are not used to select generated source modules.
- Source-copy consumers update through `regenerate-and-review`; no automatic
  source mutation is performed.

## Accepted limitations

- Historical wall-clock cost of manually creating a real slice was not
  reconstructed and is not reported as measured fact.
- V8.0 does not publish modules as NuGet packages.
- V8.0 does not provide `dotnet new` packaging or automatic multi-project
  update tooling.
- The template has no database schema, so migration and upgrade validation are
  explicitly `N/A`.
- `Projects`, `ProjectTasks` and `Notifications` remain production reference
  modules, not blindly copyable generated capabilities.
- Local Docker/Testcontainers evidence is not required for this schema-free
  template proof.

## Final release record

Complete after the pull request is merged:

- merged commit SHA: `TBD`;
- pull request: `TBD`;
- required CI run: `TBD`;
- proof artifact revision: `TBD`;
- tag: `TBD`.
