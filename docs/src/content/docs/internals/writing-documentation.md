---
title: "Writing documentation"
description: "Write and review documentation for its audience, purpose, status, terminology and visual explanation."
audience: "Maintainers"
kind: "How-to guide"
status: "Current"
---

> **Status: Current.** How-to guide for maintainers.

Use this guide when writing or reviewing Allors Documents documentation. Its language and
terminology rules also apply to code comments, diagram labels, example explanations, interface
text, commit messages and pull request descriptions. [Terminology](../terminology.md) records the
agreed names; the [documentation index](../index.md) lists the pages.

## Choose the audience and purpose

The pages in `docs/src/content/docs/` are for humans. Template authors write templates in
LibreOffice Writer: their pages describe the tag language and what a template can rely on, and
never require knowledge of the API. Developers render documents from their models and validate
templates: their pages assume the author pages and link to them. Maintainers change the library:
their pages assume both and explain ownership and design decisions; implementation depth belongs
in code comments and tests.

Use [Diátaxis](https://diataxis.fr/start-here/) as the editorial standard. Choose a primary
purpose for each instructional page, for any audience:

| Kind | Reader's need | What to write |
| --- | --- | --- |
| Tutorial | Learn through a guided experience. | A reliable path to a concrete result, with the guidance needed to complete it. |
| How-to guide | Complete a particular task. | Steps and relevant choices for someone who already has the necessary background. |
| Reference | Look up precise information. | Facts, contracts, parameters and constraints, organized for retrieval. |
| Explanation | Understand how or why something works. | Relationships, reasoning and consequences. |

Keep audience, kind and status separate: an explanation can be for authors or maintainers, and
can describe current or planned behaviour. Indexes, changelogs and decision records have their
own purposes and need no artificial Diátaxis classification. Improve existing pages
incrementally; create a folder with its first useful page, never to fill an empty category.
This follows [Diátaxis's guidance on applying the framework](https://diataxis.fr/how-to-use-diataxis/).

## State what is current

Set `title`, `description`, `audience`, `kind` and `status` in each page's frontmatter. Starlight
renders the title; start the body with its status line. For an instructional page, also identify
its kind in the index. Keep the metadata, status line and index consistent. The status describes
the content, not whether the page needs editing.

| Status | Meaning |
| --- | --- |
| Current | Describes the repository's present behaviour or an adopted working rule. |
| Planned | Agreed work that is not built yet or is only partly built. Identify which parts exist. |
| Historical | Retained for its reasoning. Link to the page containing the current truth. |

Update affected pages in the same pull request as a behaviour change. Once a plan has landed,
mark it Historical, keep its path and link to the current documentation. Use the documentation
from the relevant branch or tag; a published page for another version is not evidence of what
the checkout does.

A fact has one home. Link to its explanation instead of maintaining several descriptions of
the same rule. When a test can check a behavioural rule, add that coverage if it is missing
and name the test in the page. Treat the test as authoritative. A proposed rule that the
implementation does not yet satisfy is Planned.

## Write for the reader at hand

Template authors know Writer, not programming: explain a tag through what it prints and where
it may sit, and name the Writer dialogs that hold tags and settings. Developers are familiar
with ordinary programming concepts such as objects, interfaces, cultures and byte arrays:
explain the library's concepts through their behaviour, what owns the state, what an operation
changes and what the reader can rely on afterwards.

Use a familiar, precise term when it expresses the meaning. Keep established technical terms
when they distinguish something the reader needs to understand. Explain an unfamiliar term
where it is first needed, and link to its definition. Avoid both academic wording that adds no
precision and everyday substitutes that erase a technical distinction.

For example, introduce the formatting layers before explaining that a layer's culture resets
the cultures per kind of the layers below it. When explaining a picture binding, say which
dialog holds the tag and what happens without a picture; the [Pictures](../pictures.md) page
owns those details.

Prefer concrete subjects and verbs. Keep conditions, exceptions and uncertainty that affect
the meaning. Clarity matters more than sentence length: there is no fixed word limit, ban on
passive voice or requirement to replace a precise term with a shorter one.

## Use Oxford English

Use British English with Oxford dictionary spelling: `behaviour`, `colour` and `centre`,
together with `organize`, `standardize` and `serialization`. Keep exceptions such as `analyse`
and `advise`; do not replace every `-ise` ending mechanically. Oxford University Press
[explains the spelling distinction](https://home.oxfordowl.co.uk/spelling-spotlight-when-to-use-ize-and-ise/).

Preserve exact code identifiers, configuration keys, commands, paths, product names and
quotations, including the names of Writer dialogs and attributes as the tag language spells
them. Put identifiers in code formatting so readers can distinguish a name in the API or in a
tag from explanatory prose. A spelling correction is not permission to rename an API.

## Keep terminology stable

Use the agreed term for a concept throughout prose, code, diagrams and examples. Ordinary
wording can vary; the name of a project concept should not vary merely for stylistic variety.

Before renaming an established concept or introducing a competing name for it, discuss the
proposed meaning, the reason for the change and the affected scope with the maintainer
responsible for the work. Obtain agreement before applying it. A different responsibility may
deserve a different term; explain the distinction instead of silently substituting a name.

Record the accepted name in the terminology reference or its linked definition. Apply an
approved rename across the agreed scope, including code, documentation, diagrams, examples
and pull request text. Search for remaining uses and explain intentional exceptions, such as
historical records, external names or compatibility identifiers. Changing a concept's name
does not itself authorize a breaking API change.

## Explain visually where it helps

Choose the form that makes the subject easiest to understand. Flow, sequence, class and other
diagrams are all welcome; this is not a closed list. A visual can carry the main explanation.
Use surrounding text for definitions, conditions and details that the visual cannot convey
well. Do not add a picture merely to meet a quota.

Use Mermaid where its notation fits. For other drawings, keep an editable source and publish
SVG where suitable. Explain the direction and meaning of arrows and any unfamiliar notation.
Give every diagram an accessible title and description, as the
[formatting layers diagram](../formatting.md#document-settings) does.

Use interaction when changing an input, selecting an element or stepping through an operation
reveals something useful. Make the starting state and the effect of each action clear. Support
keyboard operation, readable labels and a useful textual equivalent; meaning must not depend
only on colour. Keep technical claims available in the page or an accompanying description so
readers and agents do not have to operate a widget to discover the contract. Videos are not
part of this documentation.

## Review the completed change

Review the changed material for its audience and purpose, factual accuracy, Oxford spelling,
agreed terms, useful visuals and working links. Include labels and example output in that
review. Verify executable examples against the relevant code and tests; do not present an
illustrative example as proof of the library's behaviour.

Editorial review is required. Prose linting is not part of the initial tooling. Automated
checks should cover concrete failures such as broken links, failed builds and incorrect
rendering. A successful automated check does not establish that an explanation is accurate
or clear.

Agent instructions belong in the repository's `AGENTS.md`. They direct agents to this guide
and the same human documentation, rather than maintaining a second set of technical facts.
The website generates its agent index and Markdown exports from the same page metadata and
rendered explanations. The [website instructions](../../../../README.md) cover building,
checking and reviewing those exports; the [decision record](documentation-website.md) records
the design of the website and where it comes from.
