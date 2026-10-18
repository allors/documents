---
title: "Documentation website"
description: "Agreed website design, its adoption from the Allors3 documentation project, implementation subtasks and validation."
audience: "Maintainers"
kind: "Decision record"
status: "Planned"
---

> **Status: Planned.** The website, its Markdown exports and the documentation workflow are built,
> and the checks that the workflow runs pass for both deployment paths. The editorial subtask is
> pending. Publication to GitHub Pages has not been run and the repository's Pages settings are unchanged.

This record describes the documentation website and where it comes from. The
[writing guide](writing-documentation.md) owns the editorial rules and
[Terminology](../terminology.md) owns the shared vocabulary. The pages live in
`docs/src/content/docs/`; the [website instructions](../../../../README.md) cover local
development, validation and publication.

## Agreed design

| Area | Decision |
| --- | --- |
| Readers | Human documentation serves three audiences: template authors, who write templates in LibreOffice Writer; developers, who render documents from their models and validate templates; and maintainers, who change the library. Each has its own navigation group. Agent instructions in `AGENTS.md` reuse the human content. |
| Site | Astro with Starlight. `docs/` is the website project, with the content in `docs/src/content/docs/`; maintainer pages sit under `internals/`. |
| Source | The project is adopted from the Allors3 documentation website, [allors/allors3 pull request 284](https://github.com/allors/allors3/pull/284): the integrations for source links, scrollable content and Markdown exports, the link checker, the page title component, the stylesheet, the tooling and browser tests and the workflow, with the same dependency versions. The values that are specific to a project changed: the site title and sidebar, the repository and deployment base in `site.mjs`, the `llms.txt` heading, the link checker's defaults, the audience names and the test fixtures. |
| Version label | Every page and export names the documented version. It is read from `version.json`, which Nerdbank.GitVersioning stamps on the packages: a prerelease version shows as unreleased work on `main`. |
| Authoring | Markdown by default; MDX where a page needs a component, with the component implementation separate from the prose. |
| Hosting | GitHub Pages, built with GitHub Actions, under `/documents/` next to the other Allors documentation areas. The workflow validates `/documents/` and `/`; publication is a manual run. |
| Visuals | Any diagram that helps explain the subject. Mermaid where suitable, rendered in the browser and verified there; editable sources and SVG for other drawings. |
| Interaction | Ordinary JavaScript or TypeScript first; no UI framework is selected and no interactive example exists yet. |
| Language and structure | Oxford English, precise practitioner terminology and Diátaxis, as the writing guide defines them. |
| Editorial enforcement | `AGENTS.md` directs agents to the writing guide and the terminology and requires editorial review. No prose linter; automated checks cover concrete failures. |

## Shared tooling

The second adoption of the Allors3 documentation project shows which of its files are generic.
The integrations (`exports.mjs`, `source-links.mjs`, `scrollable-content.mjs`), the link checker,
the page title component, the stylesheet, the tooling tests and the browser tests work unchanged
once the repository, branch, deployment base and version in `site.mjs`, the audience names in the
content schema and the `llms.txt` heading are parameters. The Allors documentation home decides
whether and where a shared implementation of them lives; until then each project keeps its copy
and records the differences here.

## Implementation and validation

The work is divided into two subtasks on one branch. Each subtask ends with a review of its
changes and validation, followed by explicit approval before the next subtask starts.

| Subtask | Scope | Validation | State |
| --- | --- | --- | --- |
| 1. Scaffold | The website project adopted from Allors3 and parametrized, the documentation workflow, the README's reference material moved page for page with its meaning preserved, the index, `AGENTS.md`, the writing guide, the terminology reference and this record. | Tooling tests, the Astro check, the production build with its exports, the link check and the browser tests, for both deployment paths, locally and in the workflow. | Complete. |
| 2. Editorial | The pages that are new writing: a how-to guide for writing a template in Writer, a how-to guide for renderers and picture processing, the design explanation, the architecture explanation and the filled terminology; the README reduced to an introduction with absolute links; the changelog entry. | The same checks, and an editorial review of every changed page and of representative exports. | Pending. |

The scaffold moves the existing pages and preserves their meaning before editorial rewrites, as
the Allors3 adoption did. Publishing the site and creating a pull request remain explicit review steps.

## Validation

The documentation workflow runs the same checks for the `/documents/` and `/` deployment paths:
the tooling tests, the Astro and TypeScript check, the production build with its exports, the link
check over the published HTML, Markdown and `llms.txt`, and the browser tests. The browser tests
cover keyboard navigation and search, Mermaid rendering in both themes, a horizontally scrolling
table at narrow width, and axe WCAG 2 A/AA checks at desktop and narrow widths. The
[website instructions](../../../../README.md) say how to run the same checks locally.

Automated checks do not establish that an explanation is accurate or clear. The writing guide
requires editorial review of changed pages and of representative exports.

## Follow-ups

- The favicon is the package icon, which is not square. Replace it with a square icon.
- Publication: enable GitHub Pages for the repository and run the workflow with publish selected, after approval.
- The policy for retaining the documentation of older released versions is not defined; the README packed into each release serves as its reference until then.
