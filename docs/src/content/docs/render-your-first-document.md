---
title: "Render your first document"
description: "Read a template, render it for a model and write the document, with the findings that say what the model could not give."
audience: "Developers"
kind: "Tutorial"
status: "Current"
---

> **Status: Current.** Tutorial for developers.

The shortest path from a template and a model to a document, with the package `Allors.Documents.Reflection` for a template over a plain class:

```csharp
using Allors.Document.OpenDocument;
using Allors.Document.Reflection;
using Allors.Document.Validation;

var template = new OpenDocumentTemplate<InvoiceModel>(templateBytes);
RenderResult result = template.Render(model);

File.WriteAllBytes("invoice.odt", result.Document);
foreach (Finding finding in result.Findings)
{
    Console.WriteLine(finding); // error: <$Invoice.Totl>: Invoice has no member Totl (content.xml paragraph 12 "Total <$Invoice.Totl>")
}
```

A template over a model type sees the public properties of `T` by their exact names and reads what is below them through reflection. The non-generic `OpenDocumentTemplate` takes any model and reads it as a dictionary unless the settings name another resolver:

```csharp
var template = new OpenDocumentTemplate(templateBytes);
var model = new Dictionary<string, object> { ["Invoice"] = invoice, ["Issuer"] = issuer };
var settings = new RenderSettings { Resolver = ReflectionResolver.Instance, Culture = new CultureInfo("nl-BE"), DateCulture = CultureInfo.InvariantCulture, Number = "N2", Date = "dd-MMM-yyyy" };
RenderResult result = template.Render(model, settings);
```

A template is read once and rendered any number of times, from any thread. It may be a package (`.odt`) or a flat document (`.fodt`), and it renders a package unless the settings ask for a flat document. Reading a template throws `InvalidDataException` when the bytes are not an OpenDocument text, and `TemplateException` when a block never ends, an end opens nothing, an else belongs to no condition or a block would cut a table; `Findings` says which tags and where. A render never throws for what the template says: a value the model cannot give prints blank, and `Findings` reports it.
