---
title: "Maps"
description: "Translating values into the template's own words with a map table and the map attribute."
audience: "Template authors"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for template authors.

A map translates a value into text the template chooses, as `true` and `false` do for a boolean: titles, labels, words in the document's language. It is a table of two columns anywhere in the template, body, header or footer, whose first cell holds `<@map Name>`; the cell beside it is free for a heading. Every row below maps the text in its first cell, the key, to the text in its second; the row whose key is `default` serves every value the map has no row for. The table leaves the output. A binding prints through a map with `map="Name"`:

| `<@map Titles>` | Title |
|---|---|
| true | CREDIT NOTE |
| default | INVOICE |

```
<$Invoice.IsCreditNote map="Titles">
```

The key is the value's text under the library's defaults, as the resolver formats it: `true` and `false` for a boolean, the name of an enum, `2.5` for a number, text as it is. A null value prints blank without a lookup. A value the map has no row for prints the default row; a map without a default row lets the value print as it is, under the tag's other attributes, and reports it as `MissingKey`. A map's name is a name, case-sensitive like a path; a name no map has is reported as `UnknownMap`. The keys and texts are plain text, with their line breaks, tabs and spaces; a tag in a map, a row with one cell, an empty key and a second row for one key are reported as `MalformedTag` and the row is skipped, and a second map with one name is reported and stays in the output.
