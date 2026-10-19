---
title: "Terminology"
description: "Agreed names for templates, tags, models and findings, with links to their definitions."
audience: "Everyone"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for template authors, developers and maintainers.

Use these names consistently when discussing Allors Documents. This page records the shared
vocabulary and links to the pages that own the detailed definitions. Exact API identifiers keep
their declared spelling and capitalization.

## Templates and tags

| Term | Definition and usage |
| --- | --- |
| Template | An OpenDocument text, a package (`.odt`) or a flat document (`.fodt`), that holds tags. It is read once and rendered any number of times. See [Tags](tags.md). |
| Tag | The text in a placeholder field or a picture description that the library reads: `<$path>` with attributes, or `<@keyword>`. See [Tags](tags.md). |
| Placeholder field | The Writer field (Insert, Field, More Fields, Functions, Placeholder) that holds a tag in the text. |
| Binding | A `<$path>` tag that prints the value at its path. Use binding for the tag and value for what it prints. |
| Path | A chain of member names separated by dots, such as `Invoice.Customer.Name`; inside a loop it may start with a loop variable. |
| Block | A loop or a condition: the paragraphs or table rows between its begin tag and `<@end>`. A condition may have else branches. |
| Loop variable | The name a `<@for>` tag declares for the item at hand; a counter is the name its `index` attribute declares. See [Loops](tags.md#loops). |
| Attribute | A named value after the path of a tag, such as `format="N2"`. See [Attributes](formatting.md#attributes). |
| Document settings | The custom document properties of a template that carry its formatting defaults. See [Document settings](formatting.md#document-settings). |
| Formatting layers | The library's defaults, the caller's settings, the template's document settings and the tag's attributes, in that order; the last layer wins. See [Formatting](formatting.md). |
| Pattern | A .NET format string for dates, times, date-times and numbers; `date`, `time`, `datetime` and `number` name the document's patterns. See [Formatting](formatting.md). |
| Map | A two-column table whose first cell holds `<@map Name>`; a binding prints through it with `map="Name"`. See [Maps](maps.md). |
| Picture tag | The `<$path>` tag in the description of a picture frame, with the `empty` and `fit` attributes. See [Pictures](pictures.md). |
| Frame | The picture frame as the author drew it in Writer. Qualify a text frame when the context could be ambiguous. |
| Part | One XML file of a template: `content.xml` for the body, `styles.xml` for the styles with the headers and footers. A flat document is split into the same parts. |

## Models and rendering

| Term | Definition and usage |
| --- | --- |
| Model | What a render binds the template to: a dictionary of roots, a plain class, or a host's own objects read through a resolver. |
| Shape | The static view of a model that `Validate` walks: the members a value has, whether it is a collection, the shape of its items and the variants of an abstract shape. See [Resolve your own model](resolve-your-own-model.md). |
| Resolver | The runtime view a render reads through: how a member is read, iterated, tested, formatted, ordered and turned into a picture. See [Resolve your own model](resolve-your-own-model.md). |
| Render settings | The caller's resolver, cultures, patterns and output form, given to a template when it is read and used by every render of it. See [API](api.md). |
| Render result | The rendered document and the findings of the render, split into errors and warnings. See [API](api.md). |
| Inspection | What a template asks of its model, read without a model: the tags, the bound pictures, the maps, the document settings and the problems. See [API](api.md#inspection). |
| Validation | The check of a template against a shape, without rendering: the findings and the inspection they came from. See [Validate templates](validate-templates.md). |
| Finding | One thing wrong or doubtful, in the author's terms: its kind, severity, the tag as typed, the path, a message and the location. See [Findings](findings.md). |
| Lenient rendering | A render never throws for what the template says: what the model cannot give prints blank and is reported as a finding. Only a template whose blocks cannot be rendered throws, at `Render`; `CanRender` says so before. |
| Package and flat document | The two forms of an OpenDocument text: a zip package (`.odt`) and one XML file with the pictures embedded (`.fodt`). See [Flat documents](api.md#flat-documents). |
| Formatting | The effective cultures, patterns and words at a tag, the result of the formatting layers; also the name of the type that carries them. See [Formatting](formatting.md). |
| Renderer | Planned: a display transformation of one value that a tag selects by name in its `format` attribute, such as upper-case text or a barcode, registered by the caller. See [Renderers and picture processing](renderers.md). The type inside the library that renders a program is named `Renderer` too; the architecture page qualifies it. |
| Picture processor | Planned: the step between a picture's bytes and the package that scales or converts a picture for its frame, registered by the caller. See [Renderers and picture processing](renderers.md). |

## Inside the library

| Term | Definition and usage |
| --- | --- |
| Program | A rendering part of a template prepared for rendering: its DOM with every tag replaced by an instruction, built once per template and walked by every render. See [Architecture](internals/architecture.md). |
| Instruction | A tag in a program: what it says and where it sits, for the findings a render reports. |
| Scope | The loop variables and counters a path may start with at a point of a render or a validation, innermost first, before the root. |
