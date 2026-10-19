---
title: "Validate templates"
description: "Check a template against the shape of its model without rendering it, when it is saved and in a test over every template."
audience: "Developers"
kind: "How-to guide"
status: "Current"
---

> **Status: Current.** How-to guide for developers.

This guide shows how to check a template against the shape of its model before any data exists.
Run the check where templates enter the application, when one is saved or uploaded, and in a test
over every template the application ships, so that a typo is a failed check rather than a blank in
a document.

`Validate` checks a template against the shape of its model, without rendering and without data: every path, attribute, block and document setting. It never throws for what the template says; reading the template throws only when the bytes are not an OpenDocument text. A template whose blocks cannot be rendered is reported here too; `DictionaryShape.Open` serves as the shape when no model is at hand.

```csharp
using Allors.Documents.Reflection;

IShape model = ReflectionShape.Arguments<InvoiceModel>();
TemplateValidation validation = new OpenDocumentTemplate(templateBytes).Validate(model);

if (!validation.IsValid)
{
    foreach (Finding finding in validation.Errors)
    {
        Console.WriteLine(finding); // error: <$Invoice.Totl>: Invoice has no member Totl (content.xml paragraph 12 "Total <$Invoice.Totl>")
    }
}
```

The shape is the root the template renders against. `ReflectionShape.Arguments<T>()` is what `OpenDocumentTemplate<T>` sees: the public properties of `T` by their exact names. `ReflectionShape.Of<T>()` is an object of `T` itself, for a model rendered through `ReflectionResolver`. For a dictionary model use `DictionaryShape`: `DictionaryShape.Open` accepts every member, `new DictionaryShape(members)` knows exactly which names exist and what their shapes are, with `ValueShape.Text`, `ValueShape.Number` and the other leaves for the values. A shape has a kind, and the check goes as far as the kind tells: a loop over a value that is no collection, a date pattern on a number, a `format` on text, a sort key that is an object and a picture frame bound to text are all findings; an open shape accepts everything. A `format` is checked against the template's renderers first: a text renderer passes on any kind, a picture renderer on a binding is an error, and a picture tag's `format` must name a picture renderer of the settings. `IsValid` is true when there are no errors. Inside an `<@if>` or `<@ifdef>` block the guarded path is narrowed to the shapes that have the member, for models whose shapes declare `Variants`; the else of an `<@ifdef>` sees the shapes without it, the else of an `<@if>` sees them all, `not` swaps the two, and an else with a test of its own narrows again.
