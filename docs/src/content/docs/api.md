---
title: "API"
description: "The packages, the template class with Inspect, Validate and Render, the render settings and result, and flat documents."
audience: "Developers"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for developers.

## Packages

| Package | Holds |
|---|---|
| `Allors.Documents` | The template and its renderer, the tag language, `Inspect`, `Validate`, the model contracts (`IShape`, `IResolver`) and the dictionary shape and resolver. Targets netstandard2.0 and net10.0, is trimmable and AOT-compatible, uses no reflection and has no dependencies. |
| `Allors.Documents.Reflection` | `OpenDocumentTemplate<T>`, `ReflectionShape` and `ReflectionResolver`: templates over plain classes and dictionaries, their members read through reflection. |

## Inspect

`Inspect` reads a template without a model and lists what it asks for: every tag in document order with its part and paragraph, the bound picture frames, the maps, the document settings, and the tags it cannot read.

```csharp
TemplateInspection inspection = OpenDocumentTemplate.Inspect(templateBytes);

inspection.Paths;      // every distinct path, bindings and pictures alike
inspection.Bindings;   // the <$path> tags, each with Tag, Location and the enclosing Block
inspection.Blocks;     // the for, if and ifdef tags, each with its Children, Elses and End
inspection.Pictures;   // the frames whose description is a picture tag, each with FrameName, Tag and Location
inspection.Maps;       // the map tables, each with Name, Entries, Default and Location
inspection.Settings;   // the custom document properties
inspection.Problems;   // malformed tags, unbalanced blocks, elses without a condition and maps that cannot be read
```

A `Location` names the part (`content.xml` or `styles.xml`), the paragraph's ordinal and its text.

## Flat documents

A template may be a flat OpenDocument text (`.fodt`, File, Save As, Flat XML ODF Text Document): one XML file with the pictures embedded. `RenderSettings.Flat = true` renders one, pictures embedded as base64; the default is a package, whatever the template's form. The findings of a flat template name `content.xml` for the body and `styles.xml` for the styles with the headers and footers, the parts the engine splits it into. Embedded objects such as charts and formulas are not converted between the two forms.
