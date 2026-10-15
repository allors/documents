# Changelog

All notable changes to this project are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [semantic versioning](https://semver.org/).

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
