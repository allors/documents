# Changelog

All notable changes to this project are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [semantic versioning](https://semver.org/).

## [Unreleased]

### Changed

- **Breaking:** every type of the core sits in the namespace `Allors.Documents`, the name of the package, and the reflection package in `Allors.Documents.Reflection`; the namespaces `Allors.Document.OpenDocument`, `Allors.Document.Validation`, `Allors.Document.Inspection`, `Allors.Document.Models`, `Allors.Document.Tags` and `Allors.Document.Reflection` are gone. To upgrade, replace the `using Allors.Document.*` directives by `using Allors.Documents;` and `using Allors.Documents.Reflection;`. `TagSyntax` and `XmlPair` are internal: neither was part of the contract.
- **Breaking:** `Finding`, `Location` and `SortKey` are records: two with the same members are equal, `ToString` prints as before, and their constructors are public, so an application can build findings of its own.
- **Breaking:** a template takes its settings when it is read: `OpenDocumentTemplate(byte[] document, RenderSettings settings = null)`, `Settings`, and `Render(object model)` without a settings parameter; the properties of `RenderSettings` are set by the initializer only. Every render of a template uses the same settings; a render with other settings, another culture or a flat document, comes from another template over the same bytes. `OpenDocumentTemplate<T>` takes the same arguments and reads through `ReflectionResolver` unless the settings name a resolver.
- **Breaking:** the static `Inspect` and `Validate` are gone. `Inspection` is a property of the template, read with it, and `Validate(IShape root)` an instance operation that checks the template under its settings.
- **Breaking:** reading a template throws only `InvalidDataException`, for bytes that are no OpenDocument text. A block that never ends, an end that opens nothing, an else that belongs to no condition or a block that would cut a table no longer throw when the template is read: `Validate` reports them, with `DictionaryShape.Open` when no shape is at hand, and `Render` throws `TemplateException` with those findings.
- **Breaking:** a shape has a kind. `IShape.Kind`, a `ValueKind`, replaces `IsCollection`: `Open`, `Object`, `Collection`, or a leaf `Text`, `Number`, `Boolean`, `Date`, `Time`, `DateTime`, `Picture` or `Other`; a loop iterates a collection or an open value, and `Element` is the item of either. `DictionaryShape.Open` is open and a closed dictionary an object; `ReflectionShape` takes the kind from the type, a byte array as a picture rather than a collection of bytes, and keeps the members of every type as the resolver reads them. A shape of your own returns the kind of its unit, and `ValueShape` is the leaf: `ValueShape.Text` and the other shared ones, or `new ValueShape(kind, name)`.
- **Breaking:** `Formatting.PatternFor(ValueKind)` and `Formatting.CultureFor(ValueKind)` replace `Pattern(string)` and `CultureOf(string)`; the setting names stay as the string constants, and `Formatting.KindOfSetting(name)` maps a pattern setting to its kind.
- **Breaking:** `Validate` checks a pattern by its grammar instead of trying it on a value, so a pattern .NET prints but not as meant, such as `yyyy-MM-dd Q`, `yyyy-MM-ddTHH:mm` or `EUR`, is a `BadPattern` error. A date pattern is a standard letter or runs of `d`, `M`, `y`, `h`, `H`, `m`, `s`, `f`, `F`, `t`, `z`, `K` and `g` with separators, quoted text and `\` escapes, and an unquoted letter outside that set is an error; a number pattern is a standard letter with its precision or a custom pattern with at least one `0` or `#`. The document settings `date`, `time`, `datetime` and `number` follow the same grammar.

### Added

- `RenderResult.Errors` and `RenderResult.Warnings`: the findings of a render by severity, as `TemplateValidation` splits those of a validation. An error says a value is missing from the document; a warning says the document is complete but not as the template asked.
- Renderers: a tag's `format` attribute names a renderer before it is read as a pattern name or a .NET format string. A text renderer, `ITextRenderer`, gives the text a binding prints for its value and the text the resolver formatted it to; the built-in text renderers `upper`, `lower` and `cap`, `TextRenderers`, transform that text in the culture of the formatting, and every template has them. A picture renderer, `IPictureRenderer`, draws the picture a frame shows for its value, from the value, its text under the library's defaults and the frame's size in inches and fit, `PictureFrame`: `<$Invoice.Number format="code128">` in a picture's description. The caller lists its renderers in `RenderSettings.Renderers`; the template reads them once, a later renderer replaces an earlier one with its name, built in or not, and a null renderer or one without a name is an `ArgumentException` when the template is read. A renderer is shared by every render, from any thread.
- `RenderSettings.PictureProcessor`, an `IPictureProcessor`: the step between a bound picture's bytes and the package, which sees the bytes the model or a renderer gave, their format and the frame, and gives the bytes to store, such as the picture scaled to the frame. `PictureFormat` is public for it.
- `Validate` checks a `format` against the template's renderers: a text renderer passes on a value of any kind, a picture renderer on a binding is a `BadValue` error, and a picture tag's `format` must name a picture renderer of the settings. A picture tag that names one binds a value of any kind, a number for a barcode; without one the value must be a picture. The message of a `BadPattern` lists the text renderers.
- `RendererFailed`, a new finding kind: a renderer that threw, or a picture renderer that gave bytes in no format the library knows, is an error, and the binding prints blank or the frame has no picture; the picture processor failing is a warning, and the picture is stored as it was. A `format` that names a picture renderer on a binding, or no picture renderer on a picture tag, is a `BadValue` at render too: the binding prints without the renderer, the frame shows the resolver's picture.
- The package `Allors.Documents.Barcodes`: the picture renderers `code128`, `ean13` and `qr`, `BarcodeRenderers.All`, draw a barcode of the value's text with ZXing.Net as a PNG of the frame's size in pixels at the renderer's `Dpi`, 300 unless set; a frame without a size gets 320 by 80 pixels, `qr` 320 by 320. The quiet zone is part of the picture and the text under the bars is not. Text a symbology cannot encode is a `RendererFailed` error with the reason. The package has no imaging dependency: it writes the PNG itself.
- The package `Allors.Documents.Imaging`: `PictureScaling`, a picture processor over SkiaSharp that scales a bound picture down to the pixels its frame needs at `Dpi`, 300 unless set, by the tag's `fit`, and never up; applies the orientation a photo carries to its pixels, so the document shows it upright and the frame fits to the upright sides; keeps a PNG a PNG, stores a picture with transparency as PNG and the rest as JPEG at `JpegQuality`, 85 unless set; leaves a picture that fits upright, a result that would not be smaller, an SVG, EMF, WMF or TIFF and bytes it cannot decode as they are. An application on Linux adds `SkiaSharp.NativeAssets.Linux`.
- `Validate` checks a tag against the kind of the value at its path. A pattern of the wrong family, a date pattern on a number or `format="number"` on a date, is a `BadPattern` error. A `format` on text, a boolean, an object or a picture, or `true` and `false` words on anything but a boolean, is a `FormatIgnored` warning, a new kind: the value prints as it is. A format on a collection is checked against its items. A sort key that is an object, a collection or a picture is a `CannotSort` error before any render, and a picture frame bound to a value that is no picture a `NotAPicture` error; both were render findings only. An open value, or a value of another kind such as a `TimeSpan`, takes any pattern.
- A documentation website under `docs/`, built with Astro and Starlight from pages for template authors, developers and maintainers, with Markdown exports and an `llms.txt` index generated from the same pages; the documentation workflow validates it for the `/documents/` and `/` deployment paths. The reference the README held moved to the pages, joined by new pages on writing a template in Writer, the design, the architecture and the planned renderers. The guide on writing a template says what to enter in Writer's dialogs: `$Invoice.Number` in the Placeholder box, which Writer saves as `<$Invoice.Number>`, and the complete tag with its brackets in a picture's description. The README is an introduction that links to the pages, and `AGENTS.md` holds the agent instructions.

### Fixed

- A nested block whose begin or end tag shared a paragraph with its parent block's begin or end tag crashed when the template was read. Both layouts render, and the shared paragraph leaves the document once.

## [3.2.0] - 2026-10-16

A condition can say what to print otherwise, a loop can put text between its items, a binding can print a text of its own for a blank value, and a template can carry maps that translate values into its own words. Every addition is a tag or an attribute that meant nothing before, so a 3.1 template renders as it did.

### Added

- `<@else>` inside an `<@if>` or `<@ifdef>`: what follows, up to the `<@end>`, is kept when the condition is not. `<@else if path>` and `<@else ifdef path>` are elses with a test of their own: a condition takes the first branch whose test holds, and nothing when none does. An else sits in a paragraph or row of its own, like the begin and the end, or inline with them in one paragraph. An else outside any condition, in a loop or after the `<@else>` throws `TemplateException` when the template is read, as an unbalanced block does; an else in the paragraph or row of its block's begin or end tag throws as a block structure error.
- `not` before the path of an `<@if>`, `<@ifdef>`, `<@else if>` or `<@else ifdef>` negates the test.
- `separator=", "` on a `<@for>`: the text written between two items of a loop that sits in one paragraph, after the sort. A loop whose tags sit in paragraphs or rows of their own writes none; `Validate` reports it as `SeparatorIgnored`.
- Maps: a table of two columns whose first cell holds `<@map Name>`, anywhere in the template; every row below maps the text in its first cell, the key, to the text in its second, and the row whose key is `default` serves every other value. `<$path map="Name">` prints the text the map gives for the value, whose key is its text under the library's defaults: `true` and `false` for a boolean, the name of an enum, `2.5` for a number, text as it is. The table leaves the output. A value without a row and without a default prints as it is and is reported as `MissingKey`; a name no map has as `UnknownMap`; `Validate` reports a map no binding uses as `UnusedMap` and a map without a default row as `MapWithoutDefault`; a map that cannot be read, row by row, as `MalformedTag`.
- `blank="none"` on a binding: the text printed when what the binding would print is empty or white space: a null value, empty or white-space text, an empty collection, a path that breaks. Zero prints as a number.
- `Validate` checks each branch of a condition in its own scope: the else of an `<@ifdef>` sees the variants without the member, the else of an `<@if>` sees them all, `not` swaps the two, and an else with a test narrows again. Other text in the paragraph or row of an else is reported as `MixedText`, like the begin and the end.
- `Inspect` lists the maps as `Maps`, each with its name, entries, default and location; an else is a child of its condition, listed by `Elses`; `Tag` carries `Defined`, `Negated`, `HasTest` and `Separator`; `TagKind` gains `Else` and `Map`.

## [3.1.0] - 2026-10-16

Numbers and dates can print in cultures of their own. An installation that prints `1.250,50` the Belgian way and `14-Sep-2026` with English month names sets `Culture = nl-BE` and `DateCulture = invariant`; it needs no culture of its own, whose names would follow the operating system's data.

### Added

- The document settings `number-culture` and `date-culture`, and `RenderSettings.NumberCulture` and `RenderSettings.DateCulture`: the culture numbers print in, and the culture dates, times and date-times print in, apart from `culture`, which the rest follows, sorting included. A kind without a culture of its own follows `culture`.
- `invariant` as a culture name, in `culture`, `number-culture` and `date-culture` and in a tag's `culture` attribute: the invariant culture, whose names and separators are the same on every machine.
- The layers combine the cultures as style sheets do: a layer that sets `culture` resets the cultures per kind of the layers below it, and within one layer the culture per kind wins over `culture`, whatever the order of the properties. A tag's `culture` prints the value in that culture whatever its kind, and stays the only culture attribute a tag takes.
- `Formatting.NumberCulture`, `Formatting.DateCulture` and `Formatting.CultureOf(kind)` for resolvers that format their own values; `Formatting.WithCulture(culture, numberCulture, dateCulture)`, `Formatting.IsCultureSetting` and `Formatting.InvariantCultureName`.
- `Validate` reports `BadCulture` for a `number-culture` or `date-culture` that is no culture, and tries a document's date and number patterns in the culture of their kind.

## [3.0.0] - 2026-10-15

The StringTemplate engine is gone. A template renders through the resolver and the tag language of 2.1, which this version applies in full: attributes, `sort`, document settings and `<@ifdef>`. The package has no dependencies. To upgrade: take the document from `Render`'s result, move each picture binding from the frame's name and the image dictionary to a tag in the picture's description, pass `Culture = CultureInfo.CurrentCulture` to keep printing in the current culture, and add `using Allors.Document.Reflection` for `OpenDocumentTemplate<T>`.

### Changed

- **Breaking:** `Render(object model, RenderSettings settings = null)` returns a `RenderResult`: the `Document` bytes and the `Findings`, what the template asked of the model that it could not give. A member the model does not have prints blank and is reported as `UnknownMember`; 2.x printed blank in silence. A render never throws for what the template says.
- **Breaking:** `OpenDocumentTemplate(byte[] document)` is the only constructor: the `arguments` and the delimiters are gone. A template is read once and rendered any number of times, from any thread. Reading a template whose block never ends, whose end opens nothing or whose block would cut a table throws `TemplateException` there, with `Findings` saying which tags and where; 2.x threw a `NullReferenceException` or an `InvalidOperationException` for an unbalanced template, without saying where.
- **Breaking:** `OpenDocumentTemplate<T>` moved to the package `Allors.Documents.Reflection`, namespace `Allors.Document.Reflection`. It renders the public properties of `T` by their exact names, the root `ReflectionShape.Arguments<T>()` gives `Validate`, and reads below them through `ReflectionResolver`. The non-generic `OpenDocumentTemplate` reads its model as a dictionary unless the settings name a resolver: keys as written, no reflection. 2.x read every object below the root through reflection.
- **Breaking:** a picture binds through its description (Image, Properties, Options, Description), which holds one picture tag: `<$Issuer.Logo>`, `<$item.Photo empty="keep" fit="contain">`. A frame named `$path` is a plain frame. The bytes come from the model through `IResolver.Picture`; the format and the media type come from the bytes (PNG, JPEG, GIF, WebP, BMP, TIFF, SVG, EMF, WMF). A picture is stored once per distinct content under `Pictures/<path>.<ext>`, and template pictures no frame shows leave the package. Without a picture the frame leaves the document, a null value in silence and anything else with a finding, unless its tag says `empty="keep"`. Every frame's name is made unique in the document, as Writer requires.
- **Breaking:** `<@if>` is false for empty or white-space text and for zero, next to null, false and an empty collection. 2.x kept the block for `""` and `0`.
- **Breaking:** values print through the resolver under the effective formatting: the invariant culture unless the caller's settings, the template's document settings or the tag say otherwise; a date with the `date` pattern when it has no time of day and `datetime` otherwise, a number with the `number` pattern, a boolean as the `true` or `false` word, an enum as its name. 2.x printed every value through `ToString()` in the current culture.
- **Breaking:** no loop variable name is reserved. `Validate` no longer reports `ReservedName`.
- `<@ifdef>` keeps its block when the member exists, null included; 2.1 rendered it as `<@if>`.
- `Inspect` lists the frames whose description is a picture tag, each with its frame name, tag and attributes; 2.1 listed the frames named `$path`.
- The output reads as the template did: the XML declaration, no byte order mark, a full end tag on every element of the template, comments and processing instructions kept. A manifest the render changes is written without a byte order mark; an untouched one passes as it is.

### Added

- `RenderSettings`: the `Resolver`, the `Culture`, the patterns `Date`, `Time`, `DateTime` and `Number`, the words `True` and `False`, and `Flat`. Formatting layers from the library's defaults through the caller's settings, the template's custom document properties and the tag's attributes; the last layer wins.
- The attributes and settings of 2.1 take effect: `format`, `culture`, `true` and `false` on a binding, `sort` on a loop with a stable sort, and the document settings `culture`, `date`, `time`, `datetime`, `number`, `true` and `false`.
- `<@for item Items index="n">` declares a counter, read as `<$n>`: 1 for the first item in the loop's order. It prints as a plain integer unless its tag carries a `format`, so a document-wide number pattern does not turn line numbers into `1.00`.
- `empty="remove|keep"` and `fit="fill|contain|width|height"` on a picture tag. Fill, the default, stretches the picture into the frame as drawn; contain shrinks the side that would overflow; width and height keep that side and let the other follow the picture's proportions, which may grow the frame or its row. The proportions come from the picture's header; bytes that do not tell their size fill and report `CannotFit`.
- Flat OpenDocument texts (`.fodt`) as templates, and as output with `RenderSettings.Flat`: one XML file with the pictures embedded as base64. The findings of a flat template name `content.xml` and `styles.xml`, the parts it is split into. Embedded objects such as charts and formulas are not converted between the two forms.
- Findings at render: `UnknownMember`, `MalformedTag`, `CannotSort` for a loop whose keys cannot be ordered, `NotAPicture` for a bound value that is no picture, `CannotFit`. `Validate` reports `BadValue` for an attribute value a tag does not take, and checks picture tags and the `index` attribute of a loop.

### Removed

- The dependency on StringTemplate4.
- `OpenDocumentTemplate.InferArguments`, the `arguments` and delimiter constructor parameters, `Render(IDictionary<string, object>, IDictionary<string, byte[]>)` returning bytes, the `Image` classes, `TemplateException.TemplateMessages` and `FindingKind.ReservedName`.

## [2.1.0] - 2026-10-14

A template can be inspected and validated against its model before it renders. Rendering is unchanged: every template renders the same document as 2.0.0.

### Added

- `OpenDocumentTemplate.Inspect(byte[])` reads a template without a model: every tag in document order with its part, paragraph and enclosing block, the picture frames bound by path, the custom document properties, and the tags it cannot read.
- `OpenDocumentTemplate.Validate(byte[], IShape)` checks a template against the shape of its model: unknown members on bindings, loops, conditions, sort keys and pictures; unbalanced, misplaced or row-crossing blocks; text in the paragraph of a block tag; unknown attributes; bad cultures and patterns; reserved loop names; loops over a value that is not a collection; unknown document settings. It never throws for what the template says.
- The model contracts `IShape` (the static view Validate walks) and `IResolver` (the runtime view a renderer reads through), the `Resolver` base class with the shared rules, `Formatting` with the document settings `culture`, `date`, `time`, `datetime`, `number`, `true` and `false`, and `DictionaryShape` for dictionary models, open or over declared members.
- The package `Allors.Documents.Reflection` with `ReflectionShape` and `ReflectionResolver`: the members of plain classes and dictionaries as the engine looks them up. `ReflectionShape.Arguments<T>()` is the root a template rendered with `OpenDocumentTemplate<T>` sees.
- The tag `<@ifdef path>` ... `<@end>`, which keeps its block when the object at hand has the member, even when the value is empty. The engine of this version cannot tell a missing member from an empty one, so it renders `<@ifdef>` as `<@if>`; the 3.0 renderer makes the difference.
- Attributes on a binding, `format`, `culture`, `true` and `false`, and `sort` on a loop, with `asc` or `desc` per key. This version parses and validates them and renders as before; the 3.0 renderer applies them.
- The package targets net10.0 next to netstandard2.0, is trimmable and AOT-compatible, and uses no reflection; the reflection classes carry `RequiresUnreferencedCode`.

### Changed

- Tags are read by a grammar instead of loose patterns. A placeholder whose text is not a well-formed tag, such as `<$>` or `<@for item>`, renders as the text the author typed and `Validate` reports it; 2.0.0 handed whatever stood between the brackets to the engine.

## [2.0.0] - 2026-10-14

The template renders as typed, and nothing more. The constructors and `Render` are unchanged.

### Changed

- **Breaking:** template text renders exactly as typed. 1.x wrote a line break before every element and closing tag, which readers showed as a space: `Supplier:` with a formatted colon printed as `Supplier :`, `(` before a tag printed as `( 21`, a word split by formatting printed as two words, and two adjacent tags printed with a space between them. A template that relied on that space between two adjacent tags needs a typed space.
- **Breaking:** spaces, tabs and line breaks in bound values render as typed, as `text:s`, `text:tab` and `text:line-break`. 1.x dropped tabs and line breaks and rendered a value of only white space empty.
- **Breaking:** a template picture that a passed image replaced stays out of the document and its manifest, unless another frame still shows it. 1.x copied every template picture into the document.
- The name of a bound picture frame is escaped as an attribute value: a `"` becomes `&quot;` and characters below U+0020 are dropped. 1.x wrote the `"` unescaped, which broke the document.

### Fixed

- A picture bound in both the body and a header or footer has one manifest entry. 1.x listed it once per part.
- A frame named after a passed image but holding no picture, such as a text box, is left alone. 1.x threw.

## [1.2.0] - 2026-10-13

### Fixed

- Static text and attribute values of a template are escaped again when the template becomes a StringTemplate. `Terms & Conditions`, `if a < b` and a link with `&` in its address rendered a malformed `content.xml`, and `Next >> page` ended the template body and threw at load.
- Every bound value is escaped, not only strings: a value whose `ToString()` contained `&` broke the document. Like strings, they lose the characters below U+0020.
- The engine's delimiters `˂` and `˃` (U+02C2 and U+02C3) and backslashes in static text render literally. The text between two delimiters silently disappeared, and `\\` collapsed to `\`.
- The `mimetype` entry is the first entry of the package, stored and without an extra field, as ODF 1.2 part 3 requires. It was the fourth entry, deflated.

### Changed

- PNG, JPEG, GIF and WebP pictures are stored in the package instead of deflated, whether they come from the template or from the images passed to `Render`. Deflating them cost about 22 ms per megabyte and saved nothing.
- A picture frame named `$path` renders its name through the same escaping as a text binding.

A template that contains none of the characters above renders the same text as 1.0.10; only the package layout (entry order and compression) differs.

## [1.0.10] - 2024-03-11

The last release before this changelog.
