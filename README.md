# Allors Documents

Embed template tags inside OpenDocument texts: write the template in LibreOffice Writer, bind it to a model, render the document.

# Status

[![Build Status](https://github.com/Allors/Documents/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Allors/Documents/actions/workflows/ci.yml)

# Packages

| Package | Holds |
|---|---|
| `Allors.Documents` | The template and its renderer, the tag language, the inspection, `Validate`, the model contracts (`IShape`, `IResolver`) and the dictionary shape and resolver. Targets netstandard2.0 and net10.0, is trimmable and AOT-compatible, uses no reflection and has no dependencies. |
| `Allors.Documents.Reflection` | `OpenDocumentTemplate<T>`, `ReflectionShape` and `ReflectionResolver`: templates over plain classes and dictionaries, their members read through reflection. |

# Usage

```csharp
using Allors.Documents;
using Allors.Documents.Reflection;

var template = new OpenDocumentTemplate<InvoiceModel>(templateBytes);
RenderResult result = template.Render(model);

File.WriteAllBytes("invoice.odt", result.Document);
foreach (Finding finding in result.Findings)
{
    Console.WriteLine(finding); // error: <$Invoice.Totl>: Invoice has no member Totl (content.xml paragraph 12 "Total <$Invoice.Totl>")
}
```

A template is read once and rendered any number of times, from any thread. A render never throws for what the template says: a value the model cannot give prints blank, and the findings say which tag and where. `Validate` reports the same before any data exists.

# Documentation

The documentation is written for template authors, developers and maintainers. The pages live under `docs/` in this repository; the links go to their sources on GitHub.

**For template authors:** [Write a template](https://github.com/allors/documents/blob/main/docs/src/content/docs/write-a-template.md) · [Tags](https://github.com/allors/documents/blob/main/docs/src/content/docs/tags.md) · [Formatting](https://github.com/allors/documents/blob/main/docs/src/content/docs/formatting.md) · [Pictures](https://github.com/allors/documents/blob/main/docs/src/content/docs/pictures.md) · [Maps](https://github.com/allors/documents/blob/main/docs/src/content/docs/maps.md) · [Findings](https://github.com/allors/documents/blob/main/docs/src/content/docs/findings.md)

**For developers:** [Render your first document](https://github.com/allors/documents/blob/main/docs/src/content/docs/render-your-first-document.md) · [Validate templates](https://github.com/allors/documents/blob/main/docs/src/content/docs/validate-templates.md) · [Resolve your own model](https://github.com/allors/documents/blob/main/docs/src/content/docs/resolve-your-own-model.md) · [Renderers and picture processing](https://github.com/allors/documents/blob/main/docs/src/content/docs/renderers.md) (planned) · [API](https://github.com/allors/documents/blob/main/docs/src/content/docs/api.md) · [Design](https://github.com/allors/documents/blob/main/docs/src/content/docs/design.md)

**For everyone:** [Terminology](https://github.com/allors/documents/blob/main/docs/src/content/docs/terminology.md) · [Documentation index](https://github.com/allors/documents/blob/main/docs/src/content/docs/index.md)

**For maintainers:** [Architecture](https://github.com/allors/documents/blob/main/docs/src/content/docs/internals/architecture.md) · [Releasing](https://github.com/allors/documents/blob/main/docs/src/content/docs/internals/releasing.md) · [Writing documentation](https://github.com/allors/documents/blob/main/docs/src/content/docs/internals/writing-documentation.md) · [Documentation website](https://github.com/allors/documents/blob/main/docs/src/content/docs/internals/documentation-website.md)

Changes are recorded in [CHANGELOG.md](https://github.com/allors/documents/blob/main/CHANGELOG.md). Agents follow [AGENTS.md](https://github.com/allors/documents/blob/main/AGENTS.md), which points to the same pages.
