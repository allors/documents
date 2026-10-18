---
title: "Documentation"
description: "Documentation for template authors, for developers who render documents and validate templates, and for maintainers of Allors Documents."
audience: "Everyone"
kind: "Index"
status: "Current"
---

> **Status: Current.**

Allors Documents renders OpenDocument texts from templates. A template is written in LibreOffice
Writer with tags in placeholder fields and in the descriptions of pictures, bound to a model and
rendered to a document. The documentation serves three audiences. Template authors write
templates in Writer. Developers render documents from their models and validate templates.
Maintainers change the library.

[README.md](../../../../README.md) introduces the packages. The
[writing guide](internals/writing-documentation.md) defines the rules for these pages;
[Terminology](terminology.md) records the agreed names. These pages are for humans. Agents use
the same technical content, following the instructions in `AGENTS.md`.

## For template authors

The following pages are written for template authors. Developers read them too.

| Page | Kind | Status | Content |
| --- | --- | --- | --- |
| [Tags](tags.md) | Reference | Current | The tag language: bindings, loops, conditions with their else branches, and where a tag may sit and how the template renders. |
| [Formatting](formatting.md) | Reference | Current | How values print: the attributes of a tag, the document settings of a template, the cultures and patterns, and the layers that combine them. |
| [Pictures](pictures.md) | Reference | Current | Binding a picture frame through its description: the picture tag, the empty and fit attributes, the formats the library knows and how pictures are stored. |
| [Maps](maps.md) | Reference | Current | Translating values into the template's own words with a map table and the map attribute. |
| [Findings](findings.md) | Reference | Current | What Validate and Render report: every kind of finding with its severity, its meaning and where it comes from. |

## For developers

| Page | Kind | Status | Content |
| --- | --- | --- | --- |
| [Render your first document](render-your-first-document.md) | Tutorial | Current | Read a template, render it for a model and write the document, with the findings that say what the model could not give. |
| [Validate templates](validate-templates.md) | How-to guide | Current | Check a template against the shape of its model without rendering it, when it is saved and in a test over every template. |
| [Resolve your own model](resolve-your-own-model.md) | How-to guide | Current | Implement the shape and the resolver that let templates read your own objects, and the rules the base resolver carries. |
| [API](api.md) | Reference | Current | The packages, the template class with Inspect, Validate and Render, the render settings and result, and flat documents. |
| [Terminology](terminology.md) | Reference | Current | Agreed names for templates, tags, models and findings, with links to their definitions. |

## For maintainers

The pages under `internals/` describe how the library is built, documented and released. An
application must not rely on them: they can change with any version.

| Page | Kind | Status | Content |
| --- | --- | --- | --- |
| [Releasing](internals/releasing.md) | How-to guide | Current | Build, test and pack with Task, how the version is stamped, and how a tag publishes the packages. |
| [Writing documentation](internals/writing-documentation.md) | How-to guide | Current | Audience, Diátaxis, status, Oxford English, stable terminology, visual explanations and review. |
| [Documentation website](internals/documentation-website.md) | Decision record | Planned | Agreed website design, its adoption from the Allors3 documentation project, implementation subtasks and validation. |

The writing guide defines [page kinds](internals/writing-documentation.md#choose-the-audience-and-purpose)
and [statuses](internals/writing-documentation.md#state-what-is-current). Audience, kind and
status describe different things: who a page serves, what it helps them do and whether it
describes current behaviour.
