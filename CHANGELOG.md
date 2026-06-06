# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Version 2 is a ground-up rewrite. The template tag syntax is unchanged, so
existing `.odt` templates keep working, but the .NET API has breaking changes —
see _Migrating from 1.x_ in the [README](README.md).

### Added

- DOM-native rendering engine that operates directly on the OpenDocument XML in
  a single pass, replacing the StringTemplate4 round-trip.
- Expression engine supporting member paths, literals (`42`, `'text'`, `true`,
  `false`, `null`), comparisons (`==`, `!=`, `<`, `>`, `<=`, `>=`), logical
  operators (`&&`, `||`, `!`) and parentheses.
- Strongly-typed templates via `OpenDocumentTemplate<T>` together with the
  `[DocumentModel]` source generator, which emits reflection-free member
  accessors.
- `LoadAsync` and `RenderAsync` overloads.
- Image substitution: frame names are treated as expressions, evaluated in loop
  scope, and matched against the supplied images.

### Changed

- **BREAKING:** replaced the StringTemplate4 engine with the DOM-native
  renderer. Rendered XML is no longer byte-identical to 1.x, but the produced
  documents are equivalent.
- **BREAKING:** namespaces moved from `Allors.Document.*` to `Allors.Documents.*`.
- **BREAKING:** templates are now created with `OpenDocumentTemplate.Load(bytes)`
  instead of `new OpenDocumentTemplate(bytes, arguments)`. Templates are
  immutable after loading and safe to render concurrently.
- **BREAKING:** `Render` now takes an `IReadOnlyDictionary<string, object?>`
  instead of `IDictionary<string, object>`.
- Retargeted the library to `net8.0` and enabled nullable reference types.

### Removed

- **BREAKING:** the StringTemplate4 dependency.
- **BREAKING:** the `arguments` / `InferArguments` formal-argument list and the
  custom delimiters. Unknown model keys are now simply ignored.

### Fixed

- Preserve numeric precision in expression comparisons.
- Escape keyword identifiers in generated model accessors.

## [1.0.0]

- Initial release (StringTemplate4-based engine).

[Unreleased]: https://github.com/Allors/Documents/compare/1.0.0...HEAD
[1.0.0]: https://github.com/Allors/Documents/releases/tag/1.0.0
