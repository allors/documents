# Changelog

All notable changes to this project are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [semantic versioning](https://semver.org/).

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
