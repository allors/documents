# Allors Documents

Generate office documents from OpenDocument (`.odt`) templates: design the
template in LibreOffice/OpenOffice, embed template tags in placeholders, and
render it against a model.

# Status

[![Build Status](https://github.com/Allors/Documents/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Allors/Documents/actions/workflows/ci.yml)

# Quickstart

```csharp
using Allors.Documents.OpenDocument;

var template = OpenDocumentTemplate.Load(File.ReadAllBytes("invoice.odt"));

var model = new Dictionary<string, object?>
{
    ["Person"] = new { FirstName = "Jane" },
    ["People"] = new[] { new { FirstName = "John" }, new { FirstName = "Jenny" } },
};

var images = new Dictionary<string, byte[]>
{
    ["logo"] = File.ReadAllBytes("logo.png"),
};

byte[] document = template.Render(model, images);
File.WriteAllBytes("rendered.odt", document);
```

Or strongly typed — mark the model with `[DocumentModel]` and the bundled
source generator resolves members without reflection:

```csharp
using Allors.Documents;
using Allors.Documents.OpenDocument;

[DocumentModel]
public class InvoiceModel
{
    public Person? Person { get; set; }
    public Person[]? People { get; set; }
}

var template = OpenDocumentTemplate<InvoiceModel>.Load(File.ReadAllBytes("invoice.odt"));
byte[] document = template.Render(new InvoiceModel { /* ... */ });
```

`[DocumentModel]` is **not** generated for generic types (placing it on one
produces a build warning). For reflection-free access to a generic model, derive
a non-generic type from the closed generic and annotate that:

```csharp
[DocumentModel]
public sealed class InvoicePage : PagedResult<Invoice> { }
```

Otherwise generic models resolve through reflection (the default — see
`UseReflectionFallback`), or register an accessor yourself with
`AccessorRegistry.Register`.

Templates are immutable after `Load` and can be rendered concurrently.

# Template syntax

Tags are written inside **placeholder fields** (LibreOffice: *Insert >
Field > More Fields > Functions > Placeholder*):

| Tag | Meaning |
|-----|---------|
| `<$Person.FirstName>` | Binding: insert the value of a member path (XML-escaped). Null or missing members render empty; use `?? 'fallback'` to substitute a default. |
| `<@if expression>` … `<@end>` | Conditional: keep the block when the expression is truthy, remove it otherwise. |
| `<@for item Collection>` … `<@end>` | Loop: repeat the block per item; `item` is in scope inside, plus `i` (1-based) and `i0` (0-based) indexes. |

Blocks span from the begin tag to the matching `<@end>`; the paragraphs (or
table rows) that carry the begin/end tags are removed from the output, and
everything between them is kept or repeated. Loops over table rows repeat the
rows between the markers.

**Expressions** support member paths, literals (`42`, `'text'`, `true`,
`false`, `null`), comparisons (`==`, `!=`, `<`, `>`, `<=`, `>=`), logical
operators (`&&`, `||`, `!`), null/blank coalescing (`??`) and parentheses:

```
<@if Person.FirstName>
<@if Invoice.Total >= 100 && !Invoice.Paid>
<$Person.MiddleName ?? '—'>
```

`a ?? b` evaluates to `a` unless `a` is *blank* — `null` or a whitespace-only
string — in which case it evaluates to `b`; use it to give an empty binding a
fallback (broader than C#'s null-only `??`).

Truthiness: `null` is false; booleans are themselves; strings are true when
non-empty; numbers when non-zero; collections when non-empty.

**Images**: give a frame the name `$expression` (LibreOffice: select image >
*Options > Name*). The expression is evaluated (per loop iteration inside a
loop) and the resulting name is matched against the images dictionary passed
to `Render`; matching frames get their picture replaced. Frames with a plain
name are matched against the images dictionary by that literal name.

# Contributing

Building, testing, and the project layout are documented in
[AGENTS.md](https://github.com/Allors/Documents/blob/main/AGENTS.md).
Notable changes are tracked in
[CHANGELOG.md](https://github.com/Allors/Documents/blob/main/CHANGELOG.md).

# Migrating from 1.x

Version 2 is a rewrite without the StringTemplate4 dependency; templates render
directly on the XML DOM.

- Namespaces moved from `Allors.Document.*` to `Allors.Documents.*`.
- `new OpenDocumentTemplate(bytes, arguments)` → `OpenDocumentTemplate.Load(bytes)`;
  the `arguments`/`InferArguments` formal-argument list is gone (unknown model
  keys are simply ignored), and so are the custom delimiters.
- `Render(IDictionary<string, object>)` → `Render(IReadOnlyDictionary<string, object?>)`;
  `RenderAsync` overloads are available.
- Template tag syntax is unchanged; existing `.odt` templates keep working.
- Rendered XML is not byte-identical to 1.x (escaping is now handled by the
  XML writer), but documents are equivalent.
