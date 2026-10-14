# Changelog

All notable changes to this project are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [semantic versioning](https://semver.org/).

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
