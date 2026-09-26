# V8 consumer template

This is the deliberately small consumer baseline used by the V8 scaffolding
proof. It demonstrates the stable parts of the starter contract:

- layered `API`, `Application`, `Domain`, `Infrastructure`, and `Shared` projects;
- one explicit MediatR composition root;
- a controller dispatching through `ISender`;
- a focused application request and infrastructure handler;
- unit and API integration tests.

The source template is distributed by copy. The supported update policy is
`regenerate-and-review`: generate a disposable project with the new template
version, compare it with the consumer's source, and port changes deliberately.
There is no runtime module installer or hidden update mechanism.

Generate a project from the repository root:

```powershell
.\scripts\New-V8StarterProject.ps1 `
  -Destination .\artifacts\v8\consumer `
  -Variant Minimal
```

Add a slice to an existing generated project:

```powershell
.\scripts\New-VsaSlice.ps1 `
  -RootPath .\artifacts\v8\consumer `
  -Module Catalog `
  -UseCase ListItems `
  -Kind Query
```

The generated handlers and results are intentionally marked as skeletons.
Replace each placeholder with the consuming project's business rule,
authorization, focused ports, transaction ownership and failure mapping before
exposing the endpoint. The generator does not make those domain decisions.

Each generated slice also receives a durable manifest under
`.starter\slices\`. The compatibility file `.starter\slice-manifest.json`
points to the most recently generated slice.

The template does not invent domain entities, persistence, authorization rules,
workers, events, or migrations. Those remain decisions of the consuming project.
