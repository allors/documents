# Changelog

All notable changes to this project are documented in this file. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [semantic versioning](https://semver.org/).

## [1.2.0] - unreleased

### Fixed

- Static text and attribute values of a template are escaped again when the template becomes a StringTemplate. `Terms & Conditions`, `if a < b` and a link with `&` in its address rendered a malformed `content.xml`, and `Next >> page` ended the template body and threw at load.
- Every bound value is escaped, not only strings: a value whose `ToString()` contained `&` broke the document.
- The engine's delimiters `˂` and `˃` (U+02C2 and U+02C3) and backslashes in static text render literally. The text between two delimiters silently disappeared, and `\\` collapsed to `\`.
- The `mimetype` entry is the first entry of the package, stored and without an extra field, as ODF 1.2 part 3 requires. It was the fourth entry, deflated.

### Changed

- PNG, JPEG, GIF and WebP pictures are stored in the package instead of deflated, whether they come from the template or from the images passed to `Render`. Deflating them cost about 22 ms per megabyte and saved nothing.
- A picture frame named `$path` renders its name through the same escaping as a text binding.

A template that contains none of the characters above renders the same text as 1.0.10; only the package layout (entry order and compression) differs.

## [1.0.10] - 2024-03-11

The last release before this changelog.
