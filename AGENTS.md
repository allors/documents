# AGENTS.md

## Direction

- Allors Documents renders OpenDocument texts from templates written in LibreOffice Writer. The
  tag language is the stable asset: templates are expensive to change, so a release never changes
  what an existing tag means without a major version and a migration note in `CHANGELOG.md`.
- The core package `Allors.Documents` has no third-party dependencies and uses no reflection. It
  targets `netstandard2.0` and `net10.0`, is trimmable and AOT-compatible. Reflection lives in
  `Allors.Documents.Reflection`; a capability that needs a third-party library comes as a separate
  package.
- A render never throws for what a template says: what the model cannot give prints blank and is
  reported as a finding. Only a template whose blocks cannot be rendered throws, at `Render`.
  `Validate` reports everything it can tell from the template and the shape of its model, that
  included.
- Describe Allors Documents independently in documentation, comments, commit messages and GitHub
  text. Do not name private projects, or the organizations and installations that use the library.
- Ask before introducing a breaking change outside an agreed major version.

## Development notes

- The solution is `src/Documents.slnx`: `Allors.Documents`, `Allors.Documents.Reflection`,
  `Allors.Documents.Barcodes`, `Allors.Documents.Imaging` and `Allors.Documents.Tests` (xunit).
  Build with `dotnet build src/Documents.slnx` and test with
  `dotnet test src/Allors.Documents.Tests`. CI runs `task ci CONFIGURATION=Release`, which builds,
  tests and packs; [Task](https://taskfile.dev) is a prerequisite for `Taskfile.yml`, not for `dotnet`.
- Error messages and findings are actionable: they say what is wrong in the author's terms and what
  to use instead.
- Follow existing patterns, naming and structure; avoid new conventions without reason.
- Tests build their templates in code; the resources under `src/Allors.Documents.Tests/Resources`
  are the few fixtures that need real files. A flat document fixture (`.fodt`) keeps its bytes as
  LibreOffice wrote them, with LF line endings, as `.gitattributes` says.

## Git

- Pull requests are the default. Creating a pull request requires approval.
- Use one branch and one pull request for an agreed body of work. Its parts land as focused
  commits on that branch. Create the pull request only when that work is complete; if one already
  exists, update it. Do not create a separate pull request for each part.
- Keep the title and description of a pull request correct for everything on its branch, and
  update them when the branch changes. Before merging, review and update both to describe the
  final changes and their validation.
- For a squash merge, use the pull request title as the commit subject and the full pull
  request description as the commit body. The title follows the conventional commit format,
  and the description says what changed and why, with no AI attribution and nothing that stops
  being true after the merge.
- Use purpose prefixes for branch names, such as `feature/`, `fix/`, `docs/` or `chore/`. Do not
  name the agent or tool that created the branch.
- No AI attribution in commits (no "Generated with", "Co-authored-by", or similar trailers).
- Keep commits focused and well-described, in the conventional commit format `type(scope): description`.

## Workflow

- Follow test-driven development. When fixing a bug, write a failing test first or amend an
  existing one. When creating a test, find a suitable existing class to add it to.
- Never change an existing test unless explicitly instructed to.
- Verify that the build succeeds and the tests are green before considering a task complete.
- Record notable changes in `CHANGELOG.md` (Keep a Changelog format) in the section of the version
  being prepared; a release adds the version and date.

## Documentation

- `docs/src/content/docs/` is human documentation for template authors, developers and
  maintainers. The [documentation index](docs/src/content/docs/index.md) lists the pages. This file
  holds agent instructions; use the human pages for technical facts rather than maintaining a
  separate account for agents.
- Before changing documentation, comments or public-facing explanations, read and apply the
  [writing guide](docs/src/content/docs/internals/writing-documentation.md) and the
  [terminology reference](docs/src/content/docs/terminology.md). The guide owns the audience,
  Diátaxis, status, Oxford English and visual explanation rules. Preserve exact identifiers,
  attribute names and external names.
- Do not rename an established concept or substitute a competing term without prior agreement.
  Explain the proposed meaning, reason and affected scope before asking. Apply an approved
  change consistently across its agreed scope, including code, prose, diagrams, examples and pull
  request text.
- Update pages made untrue by a change in the same pull request. Maintain the index, page
  statuses and links according to the writing guide. The README stays an introduction with
  absolute links to the pages, because it is packed into the NuGet package.
- Before completing work, review the changed material for factual accuracy, agreed terminology,
  Oxford spelling and clear language. Verify links and examples. Editorial review is required; no
  prose linter is part of the tooling.
- The website in `docs/` has its own checks. Run them from `docs/` before completing
  documentation work, as [docs/README.md](docs/README.md) describes: `npm test`, `npm run check`,
  `npm run build`, `npm run check:links` and `npm run test:browser`.

## Release

- Versions come from `version.json` (Nerdbank.GitVersioning). A release sets the version, commits
  `chore(release): X.Y.Z`, tags `vX.Y.Z` so that `release.yml` publishes the packages to nuget.org
  through trusted publishing, and then starts the next prerelease. The
  [releasing guide](docs/src/content/docs/internals/releasing.md) has the steps.
