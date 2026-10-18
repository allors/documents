---
title: "Write a template"
description: "Build a template in LibreOffice Writer step by step: tags in placeholder fields, repeated table rows, conditions, pictures, document settings, maps, and how to save and check the result."
audience: "Template authors"
kind: "How-to guide"
status: "Current"
---

> **Status: Current.** How-to guide for template authors.

A template is an ordinary Writer document with tags where the values go. This guide takes you
from a finished layout to a checked template. The reference pages hold the complete rules:
[Tags](tags.md) for what a tag may say, [Formatting](formatting.md) for how a value prints,
[Pictures](pictures.md), [Maps](maps.md), and [Findings](findings.md) for what a check reports.

Before you start, ask the developer who renders the template for the paths the model offers,
such as `Invoice.Number`, `Invoice.Customer.Name` and `Invoice.Lines` with `Product`, `Quantity`
and `Amount`. Write paths exactly as given: a path is case-sensitive, while keywords such as
`for` and attribute names such as `format` may be written in any case. A template prints what the
model gives and calculates nothing itself; a total, a VAT clause or a label that depends on a
condition is a value the developer adds to the model. [Design](design.md) explains why.

## Lay out the document first

Write the document as it should look, with sample text where the values go: the styles, the
tables, the headers and footers and the pictures. Then replace the sample text with tags. A render
changes nothing but the tags, so what you see in Writer is what the document keeps.

## Insert a tag

A tag sits in a placeholder field, which keeps it in one piece whatever the formatting around it:

1. Put the cursor where the value goes.
2. Choose Insert, Field, More Fields (Ctrl+F2) and open the Functions tab.
3. Select the type Placeholder and the format Text.
4. Type the tag in the Placeholder box, for example `<$Invoice.Number>`, and click Insert.

Writer shows a placeholder between angle brackets of its own, so the field reads
`<<$Invoice.Number>>` on screen; the tag is what you typed. View, Field Shadings (Ctrl+F8) shows
every field on a grey background. A click on a placeholder selects it, and what you type then
replaces the field with plain text; to change a tag, put the cursor right before the field and
choose Edit, Fields, or insert a new field and delete the old one.

## Print a value

`<$Invoice.Number>` prints the value at that path, and `<$Invoice.Customer.Name>` follows the
path down. Attributes after the path say how the value prints:

| Tag | What it does |
| --- | --- |
| `<$Invoice.Date format="dd MMMM yyyy">` | A pattern for this one value. `format="date"` uses the document's date pattern instead. |
| `<$Invoice.Total format="N2">` | Two decimals, in the culture numbers print in. |
| `<$Invoice.IsPaid true="paid" false="open">` | The words a yes or a no prints. |
| `<$Invoice.Comment blank="none">` | What prints when there is no value. |

A path the model does not have prints blank and is reported, so a typo never breaks the
document; it shows up in the check at the end. A value that is empty prints blank in silence.
[Formatting](formatting.md) lists every attribute.

## Repeat table rows

A loop repeats the rows between its two tags once per item. Give the tags rows of their own:
insert a row above the line and a row below it. Both rows leave the output and the row between
them repeats. Inside the loop, the name after `for` stands for the item at hand.

| Product | Quantity | Amount |
| --- | --- | --- |
| `<@for line Invoice.Lines>` | | |
| `<$line.Product>` | `<$line.Quantity>` | `<$line.Amount format="N2">` |
| `<@end>` | | |

Keep the row of a tag otherwise empty: everything else in the row of a block tag leaves with it,
and the check reports it. The same holds for paragraphs: a loop over paragraphs puts `<@for>` and
`<@end>` in paragraphs of their own. A loop that fits in one paragraph needs none of this and may
put text between its items: `Tags: <@for t Product.Tags separator=", "><$t><@end>.`

`sort="Product.Name"` orders the items and `index="n"` numbers them, read as `<$n>`; see
[Loops](tags.md#loops).

## Show or hide a part

`<@if Invoice.IsPaid>` keeps what follows, up to `<@end>`, when the value is true: not empty, not
zero, not a no. `<@else>` gives the other case, and `<@if not ...>` turns the test around. The
tags take paragraphs or rows of their own, like a loop's, or sit together in one paragraph:

```
<@if Invoice.IsPaid>Paid<@else>Due by <$Invoice.DueDate><@end>
```

For models whose objects do not all have the same members, `<@ifdef path>` keeps its block when
the member exists, even when it is empty. [Tags](tags.md) has the complete rules and the
`<@else if>` form.

## Bind a picture

Insert a picture where it goes, at the size it should have; it stands in for the real one. Then
put the tag in the picture's description:

1. Right-click the picture and choose Properties, or choose Format, Image, Properties.
2. Open the Options tab.
3. Type the tag in the Description box, for example `<$Issuer.Logo>` or
   `<$item.Photo fit="contain" empty="keep">`.

The Description is the longer of the two accessibility texts; the short Text Alternative stays
yours and travels to the output. LibreOffice's Alt Text dialog (Format, Alt Text) shows the same
two boxes, where the longer one is labelled Alt Text. `fit` says how the picture takes the frame
and `empty` what the frame does when there is no picture; [Pictures](pictures.md) explains both.

## Set the template's defaults

A template carries its own formatting defaults, which win over the application's settings:

1. Choose File, Properties and open the Custom Properties tab.
2. Click Add Property and give it a name, the type Text and a value, for example `culture` with
   `nl-BE`, `date` with `dd-MMM-yyyy` or `number` with `#,##0.00`.

`culture`, `number-culture`, `date-culture`, `date`, `time`, `datetime`, `number`, `true` and
`false` are the names the library knows; any other name is reported. A tag's attribute wins over
these defaults. See [Document settings](formatting.md#document-settings).

## Translate values with a map

A map prints a value as a word of the template's own: a yes as `CREDIT NOTE`, a code as a label.
Insert a table of two columns anywhere, a last page included, with `<@map Titles>` in its first
cell and a row per value: the value's text in the first cell, the text to print in the second.
The row with the key `default` serves every other value, and the table leaves the output. A
binding prints through the map with `<$Invoice.IsCreditNote map="Titles">`. [Maps](maps.md) says
how a value's key is spelled.

## Save and check

Save the template as an OpenDocument text (`.odt`), or as a Flat XML ODF Text Document (`.fodt`)
when one file that a version control system can compare suits better; both render alike. Then
give it to the application. An application built as the developer pages describe validates a
template when it is saved and reports what a render could not give; either way a finding reads
the same: the tag as you typed it, what is wrong, and where. `content.xml` is the body,
`styles.xml` the headers and footers, and the paragraph number counts from the top of that part.

```
error: <$Invoice.Customer.Email>: Customer has no member Email (content.xml paragraph 3 "Customer: <$Invoice.Customer.Name>, <$Invoice.Customer.Email>")
```

Most findings leave the document rendering, with a blank where the value would be. Two do not: a
block that never ends, and a block whose begin and end would cut a table, stop the template from
being read at all, so fix those first. [Findings](findings.md) lists every finding, its severity
and what the render does about it.
