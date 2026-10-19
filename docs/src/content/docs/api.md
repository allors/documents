---
title: "API"
description: "The packages, the template class with its inspection, Validate and Render, the render settings and result, and flat documents."
audience: "Developers"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for developers.

This page is the reference for the public API: the four packages, the template class and its
three operations, the settings a render takes, what a render returns, and the two forms of a
document. The namespace is `Allors.Documents`, the name of the package, and
`Allors.Documents.Reflection`, `Allors.Documents.Barcodes` and `Allors.Documents.Imaging` for the other packages.

## Packages

| Package | Holds |
|---|---|
| `Allors.Documents` | The template and its renderer, the tag language, the inspection, `Validate`, the model contracts (`IShape`, `IResolver`) and the dictionary shape and resolver. Targets netstandard2.0 and net10.0, is trimmable and AOT-compatible, uses no reflection and has no dependencies. |
| `Allors.Documents.Reflection` | `OpenDocumentTemplate<T>`, `ReflectionShape` and `ReflectionResolver`: templates over plain classes and dictionaries, their members read through reflection. |
| `Allors.Documents.Barcodes` | The picture renderers `code128`, `ean13` and `qr`, drawn with ZXing.Net; see [Renderers and picture processing](renderers.md#packages). |
| `Allors.Documents.Imaging` | `PictureScaling`, a picture processor over SkiaSharp that scales a picture down to its frame; see [Renderers and picture processing](renderers.md#packages). |

## Template

`OpenDocumentTemplate` reads a template from its bytes, with the settings every render uses, and
keeps it ready: `Inspection` is what it asks of a model, `Validate` checks it against a shape,
`Render` renders it for a model. A template
over a model type sees the public properties of `T` by their exact names and reads what is below them
through reflection. The non-generic `OpenDocumentTemplate` takes any model and reads it as a
dictionary unless the settings name another resolver:

```csharp
var settings = new RenderSettings { Resolver = ReflectionResolver.Instance, Culture = new CultureInfo("nl-BE"), DateCulture = CultureInfo.InvariantCulture, Number = "N2", Date = "dd-MMM-yyyy" };
var template = new OpenDocumentTemplate(templateBytes, settings);
var model = new Dictionary<string, object> { ["Invoice"] = invoice, ["Issuer"] = issuer };
RenderResult result = template.Render(model);
```

A template is read once and rendered any number of times, from any thread. It may be a package (`.odt`) or a flat document (`.fodt`), and it renders a package unless the settings ask for a flat document. Reading a template throws `InvalidDataException` when the bytes are not an OpenDocument text, and nothing else: a block that never ends, an end that opens nothing, an else that belongs to no condition or a block that would cut a table are reported by `Validate`, with `DictionaryShape.Open` as the shape when there is no model at hand, and `Render` throws `TemplateException` whose `Findings` say which tags and where. A render never throws for what the template says: a value the model cannot give prints blank, and `Findings` reports it.

A template never changes after it is read: a render keeps its own state, so renders may run concurrently, and `Inspection` and `Validate` may be read alongside them. The settings' resolver, renderers and picture processor are shared by every render of the template, from any thread, so they keep no state of a render; the model is not changed while a render reads it. A render with other settings, another culture or a flat document, comes from another template over the same bytes: reading one is cheap next to rendering it.

`OpenDocumentTemplate<T>`, in `Allors.Documents.Reflection`, takes the same settings and adds a typed
`Render(T model)`; it reads through `ReflectionResolver` unless the settings name a resolver, and
`ReflectionShape.Arguments<T>()` is the shape
that matches it for `Validate`. [Render your first document](render-your-first-document.md) walks
through both.

## Render settings

`RenderSettings` carries how a render reads and formats its model: the resolver that binds paths
to values, the caller's formatting defaults, the renderers a tag may name, the picture processor
and the output form. A template takes its settings when it is read, and every render uses them;
the properties are set by the initializer only. The
template's own document settings win over these, and a tag's attributes win over both; a layer's
culture resets the number and date cultures of the layers below it. [Formatting](formatting.md) has the rules.

| Member | Meaning |
| --- | --- |
| `Resolver` | Binds paths to the model's members; the template's own resolver when null: `DictionaryResolver`, or the reflection resolver of a template over a model type. |
| `Culture` | The culture values print in and text sorts by; the invariant culture when null. |
| `NumberCulture` | The culture numbers print in when it is not `Culture`; null when they print in the culture. |
| `DateCulture` | The culture dates, times and date-times print in when it is not `Culture`; null when they print in the culture. |
| `Date`, `Time`, `DateTime`, `Number` | The patterns for a date without a time, a time without a date, a date with a time and a number; the culture's default when null. |
| `True`, `False` | The words a true and a false value print; `true` and `false` when null. |
| `Flat` | True to render a flat document (`.fodt`), one XML file with the pictures embedded; false, the default, renders a package (`.odt`). Either way the template may be a package or a flat document. |
| `Renderers` | The renderers a tag may name in its `format` attribute, after the built-in text renderers `upper`, `lower` and `cap`, which every template has: `Renderers = [new WordsRenderer(), .. BarcodeRenderers.All]`. A later renderer replaces an earlier one with its name, built in or not; a name wins over the pattern names and over a .NET format string. A null renderer, or one without a name, is an `ArgumentException` when the template is read. [Renderers and picture processing](renderers.md) has the contracts. |
| `PictureProcessor` | What the package stores for a bound picture: the processor sees the bytes the model or a renderer gave, their format and the frame, and gives the bytes to store, such as the picture scaled to the frame; null, the default, stores the bytes as they are. |

## Render result

`RenderResult` holds `Document`, the rendered document, and `Findings`: every member the template
asked for that the model does not have, every loop that could not be ordered and every tag that
could not be read, each once, in document order, and empty when the template and the model
agree. The document is complete either way: a missing value prints blank, an unordered loop
keeps the collection's order and an unreadable tag prints as typed. `Errors` are the findings
that say a value is missing from the document, `Warnings` those that say the document is complete
but not as the template asked, as on a `TemplateValidation`.

A `Finding` has its `Kind` and `Severity`, the `Text` of the tag, frame name or setting as the
author typed it, the `Path` that was checked when the finding is about one, a `Message` and a
`Location`. A `Location` names the `Part` (`content.xml` or `styles.xml`), the `Paragraph` as a
1-based ordinal in the part, 0 outside any paragraph, and the `ParagraphText` with the tags as
typed, white space collapsed, at most 200 characters. [Findings](findings.md) lists the kinds.
`Finding`, `Location` and the `SortKey` of a loop tag are records: two with the same members are
equal, and an application may construct its own.

## Inspection

`Inspection` is what the template asks for, read without a model: every tag in document order with its part and paragraph, the bound picture frames, the maps, the document settings, and the tags it cannot read.

```csharp
TemplateInspection inspection = new OpenDocumentTemplate(templateBytes).Inspection;

inspection.Paths;      // every distinct path, bindings and pictures alike
inspection.Bindings;   // the <$path> tags, each with Tag, Location and the enclosing Block
inspection.Blocks;     // the for, if and ifdef tags, each with its Children, Elses and End
inspection.Pictures;   // the frames whose description is a picture tag, each with FrameName, Tag and Location
inspection.Maps;       // the map tables, each with Name, Entries, Default and Location
inspection.Settings;   // the custom document properties
inspection.Problems;   // malformed tags, unbalanced blocks, elses without a condition and maps that cannot be read
```

A `Location` names the part (`content.xml` or `styles.xml`), the paragraph's ordinal and its text.

## Validate

`Validate(IShape root)` checks the template against the shape of its model, every path, attribute,
pattern, block and document setting, as far as the kind of each value tells, without rendering it
and against the template's renderers, built in and from the settings, and returns a `TemplateValidation`: its `Findings` in document order,
`content.xml`, then `styles.xml`, then the document settings; `IsValid`, true when there is no
error; and the `Inspection` the findings came from. [Validate templates](validate-templates.md)
says which shape to pass and how a condition narrows what its branches see.

## Flat documents

A template may be a flat OpenDocument text (`.fodt`, File, Save As, Flat XML ODF Text Document): one XML file with the pictures embedded. `RenderSettings.Flat = true` renders one, pictures embedded as base64; the default is a package, whatever the template's form. The findings of a flat template name `content.xml` for the body and `styles.xml` for the styles with the headers and footers, the parts the engine splits it into. Embedded objects such as charts and formulas are not converted between the two forms.
