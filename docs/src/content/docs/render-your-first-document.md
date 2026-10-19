---
title: "Render your first document"
description: "Build a small invoice template in Writer, render it from a plain class, read the finding that says what the model could not give, format for an installation and validate the template without data."
audience: "Developers"
kind: "Tutorial"
status: "Current"
---

> **Status: Current.** Tutorial for developers.

In this tutorial you render an invoice from a template and a plain class, see a finding report a
path the model cannot give, format the numbers and dates for an installation, and validate the
template without data. You need a .NET project and LibreOffice Writer. The output shown is from a
run of this code against the current version of the library.

## 1. Write the template

Create a document in Writer with these paragraphs and this table, every tag in a placeholder
field (Insert, Field, More Fields, Functions, Placeholder). The examples show the fields as they
appear in the document; enter the text inside the angle brackets in Writer's Placeholder box.
[Insert a tag](write-a-template.md#insert-a-tag) has the steps.

```
Invoice <$Invoice.Number>
Date: <$Invoice.Date>
Customer: <$Invoice.Customer.Name>, <$Invoice.Customer.Email>
```

| Product | Quantity | Amount |
| --- | --- | --- |
| `<@for line Invoice.Lines>` | | |
| `<$line.Product>` | `<$line.Quantity>` | `<$line.Amount>` |
| `<@end>` | | |

```
Total: <$Invoice.Total>
<@if Invoice.IsPaid>Paid<@else>Due by <$Invoice.DueDate><@end>
```

The rows that hold the loop's tags leave the output and the row between them repeats per line.
The condition sits in one paragraph, so only its tags leave. Save the document as
`invoice-template.odt`. The template asks for the customer's email on purpose: the model will not
have one.

## 2. Describe the model

The template's paths start at `Invoice`, so the model is a class with an `Invoice` property. What
is below it is read through reflection, by the exact names of the properties.

```csharp
public sealed class InvoiceModel
{
    public Invoice Invoice { get; set; }
}

public sealed class Invoice
{
    public string Number { get; set; }
    public DateTime Date { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }
    public Customer Customer { get; set; }
    public List<Line> Lines { get; set; }
    public decimal Total { get; set; }
}

public sealed class Customer
{
    public string Name { get; set; }
}

public sealed class Line
{
    public string Product { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}
```

## 3. Render

Add the package `Allors.Documents.Reflection` to the project; it brings `Allors.Documents` with
it. Read the template once, render it for a model and write the document:

```csharp
using Allors.Document.OpenDocument;
using Allors.Document.Reflection;
using Allors.Document.Validation;

var template = new OpenDocumentTemplate<InvoiceModel>(File.ReadAllBytes("invoice-template.odt"));

var model = new InvoiceModel
{
    Invoice = new Invoice
    {
        Number = "2026-0042",
        Date = new DateTime(2026, 10, 19),
        DueDate = new DateTime(2026, 11, 18),
        Customer = new Customer { Name = "Acme NV" },
        Lines =
        [
            new Line { Product = "Widget", Quantity = 2, Amount = 1250.5m },
            new Line { Product = "Bracket", Quantity = 10, Amount = 99m },
        ],
        Total = 1349.5m,
    },
};

RenderResult result = template.Render(model);
File.WriteAllBytes("invoice.odt", result.Document);
foreach (Finding finding in result.Findings)
{
    Console.WriteLine(finding);
}
```

Open `invoice.odt` in Writer: the lines repeat, the condition printed its else branch, and the
email is blank. The console says why:

```
error: <$Invoice.Customer.Email>: Invoice.Customer has no member Email (content.xml paragraph 3 "Customer: <$Invoice.Customer.Name>, <$Invoice.Customer.Email>")
```

A render never throws for what the template says: the value the model cannot give prints blank,
and the finding names the tag, the part, the paragraph and its text. `content.xml` is the body;
the headers and footers are in `styles.xml`. The dates and numbers print in the invariant culture
and without patterns: `10/19/2026 00:00:00` and `1250.5`.

## 4. Format for the installation

Pass render settings for the culture and the patterns. The template's own document settings and a
tag's attributes win over them; [Formatting](formatting.md) explains the layers.

```csharp
using System.Globalization;

var settings = new RenderSettings
{
    Culture = new CultureInfo("nl-BE"),
    DateCulture = CultureInfo.InvariantCulture,
    Number = "N2",
    Date = "dd-MMM-yyyy",
};
result = template.Render(model, settings);
```

Now the date prints `19-Oct-2026` and the amounts `1.250,50` and `99,00`. The quantities print
`2,00` and `10,00`, because the number pattern applies to every number; a quantity that should
print without decimals takes a format of its own in the template, such as
`<$line.Quantity format="0.##">`.

## 5. Validate without data

The finding above appears when a render reaches the tag. `Validate` checks every path of the
template against the shape of the model, without data, so that the same mistake is caught when
the template is saved and in a test over every template:

```csharp
using Allors.Document.Models;

IShape shape = ReflectionShape.Arguments<InvoiceModel>();
TemplateValidation validation = OpenDocumentTemplate.Validate(File.ReadAllBytes("invoice-template.odt"), shape);
foreach (Finding finding in validation.Findings)
{
    Console.WriteLine(finding);
}
```

```
error: <$Invoice.Customer.Email>: Customer has no member Email (content.xml paragraph 3 "Customer: <$Invoice.Customer.Name>, <$Invoice.Customer.Email>")
```

`validation.IsValid` is false while there is an error. [Validate templates](validate-templates.md)
says how to validate a dictionary model and where to run the check.

## Where to go next

- A model that is not a plain class: [Resolve your own model](resolve-your-own-model.md).
- Pictures, maps and the complete tag language: the pages for template authors, starting at
  [Tags](tags.md).
- The template class, the render settings and the result: [API](api.md).
