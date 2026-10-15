# Allors Documents

Embed template tags inside OpenDocument texts: write the template in LibreOffice Writer, bind it to a model, render the document.

# Status

[![Build Status](https://github.com/Allors/Documents/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Allors/Documents/actions/workflows/ci.yml)

# Packages

| Package | Holds |
|---|---|
| `Allors.Documents` | The template and its renderer, the tag language, `Inspect`, `Validate`, the model contracts (`IShape`, `IResolver`) and the dictionary shape and resolver. Targets netstandard2.0 and net10.0, is trimmable and AOT-compatible, uses no reflection and has no dependencies. |
| `Allors.Documents.Reflection` | `OpenDocumentTemplate<T>`, `ReflectionShape` and `ReflectionResolver`: templates over plain classes and dictionaries, their members read through reflection. |

# Usage

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

A template is read once and rendered any number of times, from any thread. It may be a package (`.odt`) or a flat document (`.fodt`), and it renders a package unless the settings ask for a flat document. Reading a template throws `InvalidDataException` when the bytes are not an OpenDocument text, and `TemplateException` when a block never ends, an end opens nothing or a block would cut a table; `Findings` says which tags and where. A render never throws for what the template says: a value the model cannot give prints blank, and `Findings` reports it.

# Tags

Tags go into placeholder fields (Insert, Field, More Fields, Functions, Placeholder):

| Tag | Meaning |
|---|---|
| `<$path>` | The value at `path` in the model, for example `<$Invoice.Number>`. |
| `<@for item path>` ... `<@end>` | Repeats the paragraphs or table rows between the tags for every item of the collection at `path`; inside, `item` is the item at hand. |
| `<@if path>` ... `<@end>` | Keeps the block when the value at `path` is true: not null, not false, not empty or white-space text, not zero and not an empty collection. |
| `<@ifdef path>` ... `<@end>` | Keeps the block when the object at hand has the member at the end of `path`, even when its value is empty. Meant for models whose objects differ in their members. |

A path is a chain of member names separated by dots, such as `Invoice.Customer.Name`; inside a loop it may start with the variable of that loop or of an enclosing one. A name is letters, digits and underscores. Keywords and attribute names are not case-sensitive. A placeholder whose text is not a well-formed tag, such as `<$>` or `<@for x>`, renders as the text the author typed and is reported.

## Attributes

A tag takes attributes after its path, with values quoted by `"` or `'`:

| Tag | Attribute |
|---|---|
| `<$Invoice.Date format="yyyy-MM-dd">` | `format`: a .NET format string, or one of `date`, `time`, `datetime`, `number` to use the document's pattern for that kind. |
| `<$Total culture="en-GB">` | `culture`: the culture this value prints in, whatever its kind: a name like `nl-BE`, or `invariant`. |
| `<$Paid true="yes" false="no">` | `true`, `false`: the words a boolean prints. |
| `<@for line Invoice.Lines sort="Product.Name, Quantity desc">` | `sort`: paths relative to the item, comma separated, each optionally followed by `asc` or `desc`. The sort is stable. |
| `<@for line Invoice.Lines index="n">` | `index`: the name of a counter, read as `<$n>`: 1 for the first item in the loop's order. It prints as a plain integer unless its tag carries a `format`. |
| `<$Issuer.Logo empty="keep" fit="contain">` | On a picture tag: `empty` and `fit`, see Pictures. |

An attribute a tag of that kind does not know is a validation error.

## Document settings

A template carries its own defaults as custom document properties (File, Properties, Custom Properties): `culture`, `number-culture`, `date-culture`, `date`, `time`, `datetime`, `number`, `true` and `false`. `RenderSettings` carries the same settings for the caller. Formatting layers from the library's defaults (the invariant culture, no patterns, the words `true` and `false`) up through the caller's settings, the template's properties and the tag's attributes; the last layer wins, so a template whose `number` property says `#,##0.00` prints its numbers that way whatever the caller passed.

A culture is a .NET name such as `nl-BE` or `en-GB`, or `invariant` for the invariant culture, whose month names and separators are the same on every machine. `number-culture` and `date-culture` set one kind apart from `culture`: numbers print in `number-culture`, dates, times and date-times in `date-culture`, and the rest follows `culture`, sorting included; a kind without a culture of its own follows `culture` too. The layers combine the cultures as style sheets do: a layer that sets `culture` resets the cultures per kind of the layers below it, and within one layer a culture per kind wins over `culture`, whatever the order of the properties. So a caller that prints `1.250,50` and `14-Sep-2026` with `Culture = nl-BE` and `DateCulture = invariant` is overridden as a whole by a template whose `culture` says `en-GB`, while a template whose `date-culture` says `nl-BE` changes only the dates. A tag's `culture` prints that one value in that culture whatever its kind, and is the only culture attribute a tag takes.

## Values

- Null prints blank; text prints as it is.
- A boolean prints the `true` or `false` word.
- A date prints with the `date` pattern when it has no time of day and with the `datetime` pattern otherwise; on .NET, a `DateOnly` prints with `date` and a `TimeOnly` with `time`.
- A number prints with the `number` pattern; any other formattable value, such as a `TimeSpan` or a `Guid`, prints with the tag's `format`.
- An enum prints its name. A collection bound as a value prints its items one after the other, formatted, with nothing between them; a loop is what separates them.
- Everything else prints through `ToString()`.

Patterns are .NET format strings, applied in the culture of the value's kind: `number-culture` for a number, `date-culture` for a date or a time, and `culture` unless they say otherwise.

## Loops

A loop visits what the resolver yields for the value at its path: nothing for null, text once, the keys of a dictionary, the items of any other enumerable with null items skipped, and any other value once. `sort` orders the items before the loop runs and before the counter counts; items whose keys cannot be ordered against each other keep the collection's order, which is reported as `CannotSort`.

## Paths that break

A member the model does not have prints blank, keeps a condition false and loops over nothing; it is reported once per tag as `UnknownMember`. A null before the end of a path prints blank in silence: `<$Invoice.Customer.Name>` prints nothing when the invoice has no customer. `<@ifdef>` on a path that is null before its end is false, without a finding.

# Pictures

A picture binds through its description (Image, Properties, Options, Description), which holds exactly one picture tag:

```
<$Issuer.Logo>
<$item.Photo fit="contain" empty="keep">
```

The frame shows the picture at that path: the bytes the resolver yields for the value, in a format the bytes tell, PNG, JPEG, GIF, WebP, BMP, TIFF, SVG, EMF or WMF. The frame's name stays the author's and its alternative text stays in the output; the description that held the tag leaves it.

| Attribute | Meaning |
|---|---|
| `empty="remove"` | The default: without a picture the frame leaves the document. A null value leaves in silence; a member the model does not have, or a value that is no picture, leaves with a finding. |
| `empty="keep"` | Without a picture the frame keeps the template's picture, as the author drew it. |
| `fit="fill"` | The default: the frame stays as drawn and the picture is stretched into it. What a barcode needs. |
| `fit="contain"` | The picture keeps its proportions and is as large as fits inside the frame: the side that would overflow shrinks. |
| `fit="width"` | The frame keeps its width; its height follows the picture's proportions, and may grow. |
| `fit="height"` | The frame keeps its height; its width follows the picture's proportions, and may grow. |

The proportions come from the picture's header. Bytes that do not tell their size, and a frame without a width and a height, fill and report `CannotFit`.

A picture is stored once per distinct content, under `Pictures/<path>.<ext>`, with `.2`, `.3` for further pictures at the same path: `Pictures/Issuer.Logo.png`, `Pictures/item.Photo.jpg`, `Pictures/item.Photo.2.jpg`. Template pictures that no frame shows any more leave the package and its manifest. Every frame's name is made unique in the document (`Photo`, `Photo 2`), as Writer requires, so a bound frame may sit in a loop.

# Rendering rules

- The template renders as typed. Static text and attribute values pass unchanged, including `&`, `<` and `>`. The engine adds no white space of its own: a word split by formatting stays one word, and two adjacent tags render without a space between them.
- Bound values are escaped for XML and keep their white space: a tab becomes `text:tab`, a line break `text:line-break` and a run of spaces `text:s`, as Writer saves them. Other characters below U+0020 are dropped.
- The paragraph or table row that holds a `<@for>`, `<@if>`, `<@ifdef>` or `<@end>` tag is removed with the rest of its text, so give each tag its own paragraph or row. When both tags of a block sit in one paragraph only the tags themselves are removed, so a short condition or loop can stay inline. The begin and the end of a block sit in paragraphs of one container or in rows of one table; a block that would cut a table throws when the template is read.
- The body and the headers and footers render against the same model; a loop variable is in scope between its tags only.
- The output reads as the template did: the XML declaration, no byte order mark, a full end tag on every element of the template, comments and processing instructions kept.
- The package starts with an uncompressed `mimetype` entry. PNG, JPEG, GIF and WebP pictures are stored; everything else is deflated.

# Flat documents

A template may be a flat OpenDocument text (`.fodt`, File, Save As, Flat XML ODF Text Document): one XML file with the pictures embedded. `RenderSettings.Flat = true` renders one, pictures embedded as base64; the default is a package, whatever the template's form. The findings of a flat template name `content.xml` for the body and `styles.xml` for the styles with the headers and footers, the parts the engine splits it into. Embedded objects such as charts and formulas are not converted between the two forms.

# Inspect

`Inspect` reads a template without a model and lists what it asks for: every tag in document order with its part and paragraph, the bound picture frames, the document settings, and the tags it cannot read.

```csharp
TemplateInspection inspection = OpenDocumentTemplate.Inspect(templateBytes);

inspection.Paths;      // every distinct path, bindings and pictures alike
inspection.Bindings;   // the <$path> tags, each with Tag, Location and the enclosing Block
inspection.Blocks;     // the for, if and ifdef tags, each with its Children and End
inspection.Pictures;   // the frames whose description is a picture tag, each with FrameName, Tag and Location
inspection.Settings;   // the custom document properties
inspection.Problems;   // malformed tags and unbalanced blocks
```

A `Location` names the part (`content.xml` or `styles.xml`), the paragraph's ordinal and its text.

# Validate

`Validate` checks a template against the shape of its model, without rendering and without data: every path, attribute, block and document setting. It never throws for what the template says; it throws only when the bytes are not an OpenDocument text.

```csharp
using Allors.Document.Reflection;

IShape model = ReflectionShape.Arguments<InvoiceModel>();
TemplateValidation validation = OpenDocumentTemplate.Validate(templateBytes, model);

if (!validation.IsValid)
{
    foreach (Finding finding in validation.Errors)
    {
        Console.WriteLine(finding); // error: <$Invoice.Totl>: Invoice has no member Totl (content.xml paragraph 12 "Total <$Invoice.Totl>")
    }
}
```

The shape is the root the template renders against. `ReflectionShape.Arguments<T>()` is what `OpenDocumentTemplate<T>` sees: the public properties of `T` by their exact names. `ReflectionShape.Of<T>()` is an object of `T` itself, for a model rendered through `ReflectionResolver`. For a dictionary model use `DictionaryShape`: `DictionaryShape.Open` accepts every member, `new DictionaryShape(members)` knows exactly which names exist and what their shapes are. `IsValid` is true when there are no errors. Inside an `<@if>` or `<@ifdef>` block the guarded path is narrowed to the shapes that have the member, for models whose shapes declare `Variants`.

# Findings

A `Finding` says what is wrong in the author's terms: the kind, the severity, the tag as typed, the path, a message and the `Location`. `Validate` reports everything it can tell from the template and the shape; `Render` reports what depends on the data; the constructor throws `TemplateException` with the findings a template cannot render with.

| Finding | Severity | Meaning | From |
|---|---|---|---|
| `MalformedTag` | error | A tag, or the description of a picture, that cannot be read; it renders as typed. | Validate, Render |
| `UnbalancedBlock` | error | A block without an end, or an end without a block. | Validate, constructor |
| `BlockStructure` | error | A block whose begin and end are not in paragraphs of one container or in rows of one table. | Validate, constructor |
| `UnknownMember` | error | A path segment the model does not have, on a binding, a loop, a condition, a sort key or a picture; it prints blank. | Validate, Render |
| `NotACollection` | warning | A loop over a value that is not a collection; it runs once. | Validate |
| `UnknownAttribute` | error | An attribute a tag of that kind does not know. | Validate |
| `BadValue` | error | A value an attribute does not take, such as `empty="maybe"`. | Validate |
| `BadCulture` | error | A culture name that is no culture, in a tag's `culture` or in a document setting. | Validate |
| `BadPattern` | error | A date, time or number pattern .NET rejects, in a tag or a document setting. | Validate |
| `MixedText` | warning | Text or tags in the paragraph or row that holds a block tag; they are removed with it. | Validate |
| `UnknownSetting` | warning | A document setting the library does not know. | Validate |
| `CannotSort` | warning | A loop whose sort keys cannot be ordered against each other; it keeps the collection's order. | Render |
| `NotAPicture` | error | A value bound to a picture frame that is no picture, or bytes in a format the library does not know; the frame leaves the document. | Render |
| `CannotFit` | warning | A picture that fills its frame instead of fitting it, because its bytes do not tell their size or the frame has no size. | Render |

# Models

Two interfaces describe a model. `IShape` is the static view `Validate` walks: the members a value has, whether it is a collection, the shape of its items, and the variants of an abstract shape. `IResolver` is the runtime view the renderer reads through: `TryGetMember`, `Items`, `IsTrue`, `Format`, `Compare` and `Picture`. The `Resolver` base class carries the rules this document describes for iteration, truthiness, formatting and ordering, and takes a byte array as a picture; derive from it, implement `TryGetMember`, and override the rest for your own value types. A resolver may hand out its own value types and recognise them again when it formats, compares or iterates them, which is how a host prints an amount in its currency.

| Provider | Shape | Resolver | Members |
|---|---|---|---|
| Dictionary, in `Allors.Documents` | `DictionaryShape.Open`, `new DictionaryShape(members)` | `DictionaryResolver` | The keys of a dictionary, exactly as written, and nothing else. |
| Reflection, in `Allors.Documents.Reflection` | `ReflectionShape.Of<T>()`, `ReflectionShape.Arguments<T>()` | `ReflectionResolver` | Dictionary keys as written; on any other object the name with its first letter upper-cased or as written, as a property, a `Get` method, a `get_` method, a string indexer or a field. |

`TryGetMember` returns false for a member the scope does not have, which the renderer reports, and true with a null value for a member that is empty, which prints blank. The renderer never passes a null scope.

# Build

Prerequisites: [.NET SDK 10.0](https://dotnet.microsoft.com/download) and [Task](https://taskfile.dev).

- `task` or `task ci` — build, test and pack
- `task test` — build and run tests
- `task pack` — create the NuGet packages in `artifacts/nuget`
- `task clean` — clean build outputs

Versioning is handled by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`version.json`).

# Release

Pushing a tag `vX.Y.Z` or `vX.Y.Z-rc.N` runs `release.yml`: it builds, tests and packs, publishes both packages to nuget.org through [trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) from the `nuget-release` environment, and creates a GitHub release. The package version comes from `version.json`, so set it to the tag's version before tagging.
