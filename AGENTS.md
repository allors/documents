# AGENTS.md

## Change policy

Visibility: public. Change policy: conservative.

- Make only changes required by the explicit request. Preserve existing architecture,
  organization and conventions unless directly instructed otherwise.
- Follow existing test placement, fixtures and patterns; do not introduce a separate
  testing approach. Preserve established test expectations unless the requested change
  explicitly changes that behaviour.
- Before adding test coverage, inspect the closest existing test methods. When a method
  already checks the same behaviour across types or cases, extend it in place. Add a
  separate method only for a distinct scenario that does not fit the existing test's purpose.
- Keep unrelated test setup, timing and expectations unchanged. Investigate validation
  failures and fix regressions caused by the requested change. Report independent failures
  and resulting validation limits separately; passing validation does not authorize
  unrelated fixes.
- Before committing, review the test diff against these rules. Check every new test method
  and every change to existing setup or expectations for necessity within the requested scope.
- Edit documentation, including README files, changelogs, diagrams and guides, only when
  explicitly requested. Report related improvements or documentation implications
  without implementing them.
- An exception applies only to the change directly authorized; it does not waive the
  remaining rules. Change this policy only when explicitly instructed.

## Research records and agent memory

- Store agent research notes, external comparisons, code-evidence inventories and
  investigation checkpoints in the agent's own built-in memory outside Git. Do not create
  committed memory files or a separate shared memory store for these records.
- Do not append sections such as "Research basis and code evidence" merely to record how
  the agent investigated a task. Keep resulting guidance, designs, decisions and essential
  rationale in repository documentation when that documentation is authorized.
- Retain supporting references where a substantive documented claim depends on them.
  Include an inspected revision or date only when needed to interpret or verify that claim;
  routine investigation stamps belong in memory.
- Keep repository documentation understandable without access to any agent's memory.
  Memory is working context, not authoritative product documentation; verify remembered
  implementation details against the source relevant to the task.
- Keep agent-only handoff notes in built-in memory and provide the necessary continuation
  context in chat. If memory is unavailable, use chat and state the limitation; do not claim
  to have saved memory or substitute committed files. Explicitly requested research
  deliverables and project plans remain governed by their authorized scope.

## Working agreement

- Stay within the approved objective and preserve unrelated work. Keep explanations and
  authorized documentation concise.
- Before asking for a decision, explain it fully in chat: the mechanism and relevant code,
  consequences, options, actual costs and available measurements, and a recommendation
  including the argument against it. Ask the question in plain text at the end.
- For long work explicitly divided into subtasks, describe the current subtask's changes,
  files or systems and validation before starting. Approval of a multi-subtask plan
  authorizes only its first subtask.
- At each boundary, summarize changes and verification, describe the next subtask and its
  validation, then wait for explicit approval. Proceed across boundaries only when the
  user explicitly authorizes it for the session. Re-check materially expanded scope.
- Do not invent subtask boundaries for simple tasks or routine implementation steps.
- At agreed boundaries, assess whether a fresh session would help. Before recommending
  one, record the branch, commit SHAs, validation, uncommitted work, pending decisions and
  exact next step. Use explicitly authorized project notes for project deliverables; keep
  agent-only notes in built-in memory and provide the necessary continuation context in chat.
  Include a 2–4-line restart prompt; do not require a restart after every small task.

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
- Tests build their templates in code; the resources under `src/Allors.Documents.Tests/Resources`
  are the few fixtures that need real files. A flat document fixture (`.fodt`) keeps its bytes as
  LibreOffice wrote them, with LF line endings, as `.gitattributes` says.

## Git

- Pull requests are the default. Creating or merging a pull request requires explicit
  approval. Keep one branch and one PR for an agreed body of work, with focused commits;
  open the PR when that work is complete.
- Use purpose prefixes such as `feature/`, `fix/`, `docs/` or `chore/`, not agent/tool names.
  Use conventional commits (`type(scope): description`) with no AI attribution.
- Keep the PR title and body accurate for the final diff. Update them when the branch's
  scope changes, including after review commits; describe the resulting behaviour and
  validation, not the development history.
- Default to squash merge. Use the PR title as the commit subject and its full description
  as the body, with wording that remains true after the merge.

## Workflow

- Follow test-driven development for requested behaviour changes. Add a failing regression case
  in a suitable existing class before implementing a fix.
- For code changes, verify that the build succeeds and the tests are green before considering
  the task complete.
- When changelog work is explicitly requested, use Keep a Changelog format in the section of
  the version being prepared, with the version and date for a release.

## Documentation

- `docs/src/content/docs/` is human documentation for template authors, developers and
  maintainers. The [documentation index](docs/src/content/docs/index.md) lists the pages. This file
  holds agent instructions; use the human pages for technical facts rather than maintaining a
  separate account for agents.
- For explicitly requested documentation work, read and apply the
  [writing guide](docs/src/content/docs/internals/writing-documentation.md) and the
  [terminology reference](docs/src/content/docs/terminology.md). The guide owns the audience,
  Diátaxis, status, Oxford English and visual explanation rules. Preserve exact identifiers,
  attribute names and external names.
- Do not rename an established concept or substitute a competing term without prior agreement.
  Explain the proposed meaning, reason and affected scope before asking. Apply an approved
  change consistently within the explicitly authorized scope; report implications outside it.
- Within authorized documentation work, maintain the affected index, page statuses and links
  according to the writing guide. The README stays an introduction with absolute links to the
  pages, because it is packed into the NuGet package.
- Before completing authorized documentation work, review the changed material for factual
  accuracy, agreed terminology, Oxford spelling and clear language. Verify links and examples.
  Editorial review is required; no prose linter is part of the tooling.
- For explicitly requested website documentation changes, run the checks from `docs/`, as
  [docs/README.md](docs/README.md) describes: `npm test`, `npm run check`,
  `npm run build`, `npm run check:links` and `npm run test:browser`.

## Release

- Versions come from `version.json` (Nerdbank.GitVersioning). A release sets the version, commits
  `chore(release): X.Y.Z`, tags `vX.Y.Z` so that `release.yml` publishes the packages to nuget.org
  through trusted publishing, and then starts the next prerelease. The
  [releasing guide](docs/src/content/docs/internals/releasing.md) has the steps.
