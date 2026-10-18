---
title: "Findings"
description: "What Validate and Render report: every kind of finding with its severity, its meaning and where it comes from."
audience: "Template authors and developers"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for template authors and developers.

A `Finding` says what is wrong in the author's terms: the kind, the severity, the tag as typed, the path, a message and the `Location`. `Validate` reports everything it can tell from the template and the shape; `Render` reports what depends on the data; the constructor throws `TemplateException` with the findings a template cannot render with.

| Finding | Severity | Meaning | From |
|---|---|---|---|
| `MalformedTag` | error | A tag, the description of a picture, or a map table that cannot be read; a tag renders as typed, a map row is skipped. | Validate, Render |
| `UnbalancedBlock` | error | A block without an end, an end without a block, or an else that belongs to no condition: outside any, in a loop, or after the `<@else>`. | Validate, constructor |
| `BlockStructure` | error | A block whose begin, elses and end are not in paragraphs of one container or in rows of one table. | Validate, constructor |
| `UnknownMember` | error | A path segment the model does not have, on a binding, a loop, a condition, a sort key or a picture; it prints blank. | Validate, Render |
| `NotACollection` | warning | A loop over a value that is not a collection; it runs once. | Validate |
| `UnknownAttribute` | error | An attribute a tag of that kind does not know. | Validate |
| `BadValue` | error | A value an attribute does not take, such as `empty="maybe"`. | Validate |
| `BadCulture` | error | A culture name that is no culture, in a tag's `culture` or in a document setting. | Validate |
| `BadPattern` | error | A date, time or number pattern .NET rejects, in a tag or a document setting. | Validate |
| `MixedText` | warning | Text or tags in the paragraph or row that holds a block tag; they are removed with it. | Validate |
| `UnknownSetting` | warning | A document setting the library does not know. | Validate |
| `CannotSort` | warning | A loop whose sort keys cannot be ordered against each other; it keeps the collection's order. | Render |
| `NotAPicture` | error | A value bound to a picture frame that is no picture, or bytes in a format the library does not know; the frame leaves the document. | Render |
| `CannotFit` | warning | A picture that fills its frame instead of fitting it, because its bytes do not tell their size or the frame has no size. | Render |
| `UnknownMap` | error | A `map` attribute that names no map of the template; the value prints as it is. | Validate, Render |
| `MissingKey` | warning | A value a map has no row for, in a map without a default row; the value prints as it is. | Render |
| `UnusedMap` | warning | A map no binding prints through. | Validate |
| `MapWithoutDefault` | warning | A map without a default row. | Validate |
| `SeparatorIgnored` | warning | A separator on a loop whose tags sit in paragraphs or rows of their own; nothing is written between its items. | Validate |
