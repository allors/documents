# AGENTS.md

Contributor and agent guide for **Allors Documents** — a .NET library that
renders OpenDocument (`.odt`) templates against a model. For end-user usage,
template tag syntax, and 1.x→2.x migration, see [README.md](README.md); this
file is about navigating and building the code.

## Project layout

Solution: `src/Documents.slnx`. Three projects:

| Project | Role | Target |
|---------|------|--------|
| `src/Allors.Documents` | Shipping library (the NuGet package) | `net8.0` |
| `src/Allors.Documents.Generators` | Roslyn incremental source generator, shipped inside the library package as an analyzer | `netstandard2.0` |
| `src/Allors.Documents.Tests` | xUnit test suite | `net10.0` |

The library exposes its internals to the test project via `InternalsVisibleTo`.

## Build & test

Prerequisite: [.NET SDK 10.0](https://dotnet.microsoft.com/download).

- `dotnet build src/Documents.slnx` — build the solution
- `dotnet test src/Allors.Documents.Tests` — build and run tests
- `dotnet pack src/Allors.Documents --output artifacts/nuget` — produce the `.nupkg`/`.snupkg`
- `dotnet clean src/Documents.slnx` — clean build outputs

CI (`.github/workflows/ci.yml`) runs the same build → test → pack sequence in
`Release`. Shared MSBuild settings live in `src/Directory.Build.props`:
`<Nullable>enable</Nullable>` and `<LangVersion>latest</LangVersion>` apply to
every project.

**Versioning** is computed by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)
from `version.json` and git height — do **not** hand-edit version numbers in the
`.csproj` files.

## Architecture

`OpenDocumentTemplate.Load(bytes)` parses and validates a template up front
(it's immutable afterward and safe to render concurrently); `Render(model,
images?)` produces the output document. The flow:

1. `OpenDocumentPackage` unzips the `.odt` and reads `content.xml`, `styles.xml`,
   and `META-INF/manifest.xml`; other entries are kept verbatim.
2. `TagScanner` walks the XML DOM, pairs `<@for>`/`<@if>` … `<@end>` into a block
   tree, and collects bindings + image-frame references, using the regexes in
   `TagSyntax`.
3. `ExpressionParser` / `ExpressionLexer` parse expressions (cached by text).
4. `OpenDocumentRenderer` mutates the DOM in one pass: substitutes bindings
   (XML-escaped), evaluates frame names and replaces pictures via
   `OpenDocumentImageProcessor`, and expands if/for blocks. `RenderScope` holds
   loop variables; `ValueAccessor` resolves member paths through
   `AccessorRegistry` (generated accessors for `[DocumentModel]` types, otherwise
   reflection).

Validation happens at **load** time; failures throw `TemplateException` carrying
`TemplateError`s.

### Key files

| Concern | Path (under `src/Allors.Documents/` unless noted) |
|---------|------|
| Public API | `OpenDocument/OpenDocumentTemplate.cs`, `OpenDocument/OpenDocumentTemplate{T}.cs` |
| Abstractions | `Abstractions/` (`IDocumentTemplate`, `DocumentModelAttribute`, `TemplateException`) |
| Tag scanning | `Templating/TagScanner.cs`, `Templating/TagSyntax.cs` |
| Expressions | `Expressions/ExpressionParser.cs`, `Expressions/ExpressionLexer.cs`, `Expressions/RenderScope.cs`, `Expressions/ValueAccessor.cs`, `Expressions/AccessorRegistry.cs` |
| Rendering | `OpenDocument/OpenDocumentRenderer.cs` |
| Package / images | `OpenDocument/OpenDocumentPackage.cs`, `OpenDocument/OpenDocumentImageProcessor.cs` |
| Source generator | `src/Allors.Documents.Generators/ModelBindingGenerator.cs` |
| Build config | `src/Directory.Build.props` |

## Conventions

- **One public type per file.** Folders are functional groups, not namespaces:
  `Allors.Documents` is the public surface; `.OpenDocument`, `.Expressions`, and
  `.Templating` are implementation.
- **Source generator wiring.** The library references the generator with
  `PrivateAssets="all" ReferenceOutputAssembly="false"` and packs its DLL to
  `analyzers/dotnet/cs`; the test project references it with
  `OutputItemType="Analyzer"`. After editing the generator, rebuild so consuming
  projects pick up regenerated accessors (generated into
  `Allors.Documents.Generated`).
- **Tests** mirror the source folders (`OpenDocument/`, `Templating/`,
  `Expressions/`), use the `*Tests` suffix and xUnit + XMLUnit for XML asserts.
  `OpenDocument/Odt.cs` is the helper for building and reading back documents;
  sample fixtures (`EmbeddedTemplate.odt`, `logo.png`, …) live in `Resources/`.
- **Commits** follow Conventional Commits: `type(scope): description`.
