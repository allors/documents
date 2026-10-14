# Allors Documents

Embed template tags inside open office documents. 

# Status

[![Build Status](https://github.com/Allors/Documents/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Allors/Documents/actions/workflows/ci.yml)

# Packages

| Package | Holds |
|---|---|
| `Allors.Documents` | The template, the tag language, `Inspect`, `Validate`, the model contracts (`IShape`, `IResolver`) and the dictionary shape. Targets netstandard2.0 and net10.0, is trimmable and AOT-compatible, and uses no reflection. |
| `Allors.Documents.Reflection` | `ReflectionShape` and `ReflectionResolver`: the members of plain classes and dictionaries, read through reflection with the rules the renderer uses. Needed to validate a template against a model type. |

# Usage

Write the template in LibreOffice Writer. Tags go into placeholder fields (Insert, Field, More Fields, Functions, Placeholder):

| Tag | Meaning |
|---|---|
| `<$path>` | The value at `path` in the model, for example `<$Invoice.Number>`. |
| `<@for item path>` ... `<@end>` | Repeats the paragraphs or table rows between the tags for every element of the collection at `path`. |
| `<@if path>` ... `<@end>` | Keeps the paragraphs or table rows between the tags when the value at `path` is not null, not false and not an empty collection. |
| `<@ifdef path>` ... `<@end>` | Keeps the block when the object at hand has the member at the end of `path`, even when its value is empty. Meant for models whose objects differ in their members. |

A path is a chain of member names separated by dots, such as `Invoice.Customer.Name`; inside a loop it starts with the loop variable. Keywords are not case-sensitive. A loop variable may not be one of the names the engine reserves: `default`, `first`, `group`, `if`, `implements`, `interface`, `last`, `length`, `optional`, `rest`, `strip`, `super`, `trunc`, `else`, `endif`.

A picture frame named `$path` shows the image whose name is the value at `path`; pass the images to `Render` by name.

```csharp
var template = new OpenDocumentTemplate<InvoiceModel>(templateBytes);
byte[] document = template.Render(model, new Dictionary<string, byte[]> { ["logo"] = logoPng });
```

With a dictionary as the model, name the arguments: `new OpenDocumentTemplate(templateBytes, "Invoice,Logo").Render(dictionary, images)`.

A placeholder whose text is not a well-formed tag, such as `<$>` or `<@for x>`, renders as the text the author typed; `Validate` reports it.

## Attributes and document settings

A binding takes attributes after its path, a loop takes `sort`:

| Tag | Attributes |
|---|---|
| `<$Invoice.Date format="yyyy-MM-dd">` | `format`: a .NET format string, or one of `date`, `time`, `datetime`, `number` to use the document's pattern for that kind. |
| `<$Total culture="en-GB">` | `culture`: the culture for this value. |
| `<$Paid true="yes" false="no">` | `true`, `false`: the words a boolean prints. |
| `<@for line Invoice.Lines sort="Product.Name, Quantity desc">` | `sort`: paths relative to the item, comma separated, each optionally followed by `asc` or `desc`. |

Values are quoted with `"` or `'`. An attribute a tag does not know is a validation error.

A template carries its own defaults as custom document properties (File, Properties, Custom Properties): `culture`, `date`, `time`, `datetime`, `number`, `true` and `false`. Formatting layers from the library's defaults up through the caller's settings, the template's properties and the tag's attributes; the last layer wins.

**In 2.1 the renderer applies none of this yet.** Attributes, `sort` and document settings are parsed and validated, and the engine still prints every value through `ToString()` in the current culture and loops in the collection's order. `<@ifdef>` renders as `<@if>`, because the engine cannot tell a missing member from an empty one. The 3.0 renderer honours all of it.

## Rendering rules

- The template renders as typed. Static text and attribute values pass unchanged, including `&`, `<`, `>`, `"`, backslashes and the characters `˂` and `˃` (U+02C2 and U+02C3) that the engine uses as its delimiters. The engine adds no white space of its own: a word split by formatting stays one word, and two adjacent tags render without a space between them.
- Bound values are escaped for XML and keep their white space: a tab becomes `<text:tab/>`, a line break `<text:line-break/>` and a run of spaces `<text:s/>`, as Writer saves them. Other characters below U+0020 are dropped. Booleans render as `true` or `false`; other values render through `ToString()` in the current culture.
- The paragraph or table row that holds a `<@for>`, `<@if>` or `<@end>` tag is removed with the rest of its text, so give each tag its own paragraph or row. When both tags of a block sit in one paragraph only the tags themselves are removed, so a short condition or loop can stay inline.
- The package starts with an uncompressed `mimetype` entry. PNG, JPEG, GIF and WebP pictures are stored; everything else is deflated.

# Inspect

`Inspect` reads a template without a model and lists what it asks for: every tag in document order with its part and paragraph, the picture frames bound by path, the document settings, and the tags it cannot read.

```csharp
TemplateInspection inspection = OpenDocumentTemplate.Inspect(templateBytes);

inspection.Paths;      // every distinct path, bindings and pictures alike
inspection.Bindings;   // the <$path> tags, each with Tag, Location and the enclosing Block
inspection.Blocks;     // the for, if and ifdef tags, each with its Children and End
inspection.Pictures;   // the frames named $path
inspection.Settings;   // the custom document properties
inspection.Problems;   // malformed tags and unbalanced blocks
```

A `Location` names the part (`content.xml` or `styles.xml`), the paragraph's ordinal and its text. Only frames named `$path` are listed; a frame bound by its plain name is not.

# Validate

`Validate` checks a template against the shape of its model, without rendering and without data: every path, attribute, block and document setting. It never throws for what the template says; it throws only when the package is not an OpenDocument text.

```csharp
using Allors.Document.Reflection;

IShape model = ReflectionShape.Arguments<InvoiceModel>();
TemplateValidation validation = OpenDocumentTemplate.Validate(templateBytes, model);

if (!validation.IsValid)
{
    foreach (Finding finding in validation.Errors)
    {
        Console.WriteLine(finding); // error: <$Invoice.Total>: InvoiceModel has no member Total (content.xml paragraph 12 "Total <$Invoice.Total>")
    }
}
```

`ReflectionShape.Arguments<T>()` is the root a template rendered with `OpenDocumentTemplate<T>` sees: the public properties of `T` by their exact names. Below the root, members follow the renderer's lookup: the name with its first letter upper-cased or as written, as a property, a `Get` method, a `get_` method, a string indexer or a field. For a dictionary model use `DictionaryShape`: `DictionaryShape.Open` accepts every member, `new DictionaryShape(members)` knows exactly which names exist.

| Finding | Severity | Meaning |
|---|---|---|
| `MalformedTag` | error | A tag or a bound frame name that cannot be read. |
| `UnbalancedBlock` | error | A block without an end, or an end without a block. |
| `UnknownMember` | error | A path segment the model does not have, on a binding, a loop, a condition, a sort key or a picture. |
| `NotACollection` | warning | A loop over a value that is not a collection; it runs once. |
| `UnknownAttribute` | error | An attribute a tag of that kind does not know. |
| `BadCulture` | error | A culture name that is no culture. |
| `BadPattern` | error | A date, time or number pattern .NET rejects, in a tag or a document setting. |
| `ReservedName` | error | A loop variable the engine reserves. |
| `BlockStructure` | error | A block whose begin and end are not in paragraphs of one container or in rows of one table. |
| `MixedText` | warning | Text or tags in the paragraph or row that holds a block tag; they are removed with it. |
| `UnknownSetting` | warning | A document setting the library does not know. |

`IsValid` is true when there are no errors. Inside an `<@if>` or `<@ifdef>` block the guarded path is narrowed to the shapes that have the member, for models whose shapes declare `Variants`.

# Models

Two interfaces describe a model. `IShape` is the static view used by `Validate`: the members a value has, whether it is a collection and what its elements are. `IResolver` is the runtime view a renderer uses: read a member, iterate a loop, test a condition, format and compare values, get a picture. The `Resolver` base class carries the shared rules, and a host can derive from it to bind templates to its own objects. In 2.1 only `Validate` uses a shape; the renderer still binds through its engine. The 3.0 renderer resolves through `IResolver`.

# Build

Prerequisites: [.NET SDK 10.0](https://dotnet.microsoft.com/download) and [Task](https://taskfile.dev).

- `task` or `task ci` — build, test and pack
- `task test` — build and run tests
- `task pack` — create the NuGet packages in `artifacts/nuget`
- `task clean` — clean build outputs

Versioning is handled by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`version.json`).

# Release

Pushing a tag `vX.Y.Z` or `vX.Y.Z-rc.N` runs `release.yml`: it builds, tests and packs, publishes both packages to nuget.org through [trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) from the `nuget-release` environment, and creates a GitHub release. The package version comes from `version.json`, so set it to the tag's version before tagging.
