---
title: "Tags"
description: "The tag language: bindings, loops, conditions with their else branches, and where a tag may sit and how the template renders."
audience: "Template authors"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for template authors.

Tags go into placeholder fields (Insert, Field, More Fields, Functions, Placeholder):

| Tag | Meaning |
|---|---|
| `<$path>` | The value at `path` in the model, for example `<$Invoice.Number>`. |
| `<@for item path>` ... `<@end>` | Repeats the paragraphs or table rows between the tags for every item of the collection at `path`; inside, `item` is the item at hand. |
| `<@if path>` ... `<@end>` | Keeps the block when the value at `path` is true: not null, not false, not empty or white-space text, not zero and not an empty collection. |
| `<@ifdef path>` ... `<@end>` | Keeps the block when the object at hand has the member at the end of `path`, even when its value is empty. Meant for models whose objects differ in their members. |
| `<@if not path>`, `<@ifdef not path>` | `not` before the path negates the test: the block is kept when the value is false, or when the member is missing. |
| `<@else>` | Inside an `<@if>` or `<@ifdef>`: what follows, up to the `<@end>`, is kept when the condition is not. |
| `<@else if path>`, `<@else ifdef path>` | An else with a test of its own, `not` allowed: a condition takes the first branch whose test holds, and nothing when none does. An `<@else>` without a test comes last. |
| `<@map Name>` | In the first cell of a two-column table: the table is a map, see Maps. |

A path is a chain of member names separated by dots, such as `Invoice.Customer.Name`; inside a loop it may start with the variable of that loop or of an enclosing one. A name is letters, digits and underscores. Keywords and attribute names are not case-sensitive. A placeholder whose text is not a well-formed tag, such as `<$>` or `<@for x>`, renders as the text the author typed and is reported.

```
<@if Invoice.IsCreditNote>CREDIT NOTE<@else>INVOICE<@end>
<@ifdef item.Description><$item.Description><@else><$item.Product.Name><@end>
<@if Issuer.Bank.Swift><$Issuer.Bank.Swift><@else if Issuer.Bank.Bic><$Issuer.Bank.Bic><@else>n/a<@end>
```

## Loops

A loop visits what the resolver yields for the value at its path: nothing for null, text once, the keys of a dictionary, the items of any other enumerable with null items skipped, and any other value once. `sort` orders the items before the loop runs and before the counter counts; items whose keys cannot be ordered against each other keep the collection's order, which is reported as `CannotSort`. `separator` is written between the items, after the sort: `Tags: <@for t Product.Tags separator=", "><$t><@end>.` prints `Tags: a, b, c.`

## Paths that break

A member the model does not have prints blank, keeps a condition false and loops over nothing; it is reported once per tag as `UnknownMember`. A null before the end of a path prints blank in silence: `<$Invoice.Customer.Name>` prints nothing when the invoice has no customer. `<@ifdef>` on a path that is null before its end is false, without a finding.

## Rendering rules

- The template renders as typed. Static text and attribute values pass unchanged, including `&`, `<` and `>`. The engine adds no white space of its own: a word split by formatting stays one word, and two adjacent tags render without a space between them.
- Bound values are escaped for XML and keep their white space: a tab becomes `text:tab`, a line break `text:line-break` and a run of spaces `text:s`, as Writer saves them. Other characters below U+0020 are dropped.
- The paragraph or table row that holds a `<@for>`, `<@if>`, `<@ifdef>`, `<@else>` or `<@end>` tag is removed with the rest of its text, so give each tag its own paragraph or row. When all tags of a block sit in one paragraph only the tags themselves are removed, so a short condition or loop can stay inline. The begin, the elses and the end of a block sit in paragraphs of one container or in rows of one table; a block that would cut a table, and an else in the paragraph or row of its block's begin or end tag, throw when the template is read.
- The body and the headers and footers render against the same model; a loop variable is in scope between its tags only.
- The output reads as the template did: the XML declaration, no byte order mark, a full end tag on every element of the template, comments and processing instructions kept.
- The package starts with an uncompressed `mimetype` entry. PNG, JPEG, GIF and WebP pictures are stored; everything else is deflated.
