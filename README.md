# Allors Documents

Embed template tags inside open office documents. 

# Status

[![Build Status](https://github.com/Allors/Documents/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Allors/Documents/actions/workflows/ci.yml)

# Usage

Write the template in LibreOffice Writer. Tags go into placeholder fields (Insert, Field, More Fields, Functions, Placeholder):

| Tag | Meaning |
|---|---|
| `<$path>` | The value at `path` in the model, for example `<$Invoice.Number>`. |
| `<@for item path>` ... `<@end>` | Repeats the paragraphs or table rows between the tags for every element of the collection at `path`. |
| `<@if path>` ... `<@end>` | Keeps the paragraphs or table rows between the tags when the value at `path` is not null, not false and not an empty collection. |

A picture frame named `$path` shows the image whose name is the value at `path`; pass the images to `Render` by name.

```csharp
var template = new OpenDocumentTemplate<InvoiceModel>(templateBytes);
byte[] document = template.Render(model, new Dictionary<string, byte[]> { ["logo"] = logoPng });
```

With a dictionary as the model, name the arguments: `new OpenDocumentTemplate(templateBytes, "Invoice,Logo").Render(dictionary, images)`.

## Rendering rules

- Static text and attribute values render unchanged, including `&`, `<`, `>`, `"`, backslashes and the characters `˂` and `˃` (U+02C2 and U+02C3) that the engine uses as its delimiters.
- Bound values are escaped for XML. Characters below U+0020 (tab, newline) are dropped and a value of only white space renders empty. Booleans render as `true` or `false`; other values render through `ToString()` in the current culture.
- The paragraph or table row that holds a `<@for>`, `<@if>` or `<@end>` tag is removed with the rest of its text, so give each tag its own paragraph or row.
- The package starts with an uncompressed `mimetype` entry. PNG, JPEG, GIF and WebP pictures are stored; everything else is deflated.

# Build

Prerequisites: [.NET SDK 10.0](https://dotnet.microsoft.com/download) and [Task](https://taskfile.dev).

- `task` or `task ci` — build, test and pack
- `task test` — build and run tests
- `task pack` — create the NuGet packages in `artifacts/nuget`
- `task clean` — clean build outputs

Versioning is handled by [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (`version.json`).

# Release

Pushing a tag `vX.Y.Z` or `vX.Y.Z-rc.N` runs `release.yml`: it builds, tests and packs, publishes the package to nuget.org through [trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) from the `nuget-release` environment, and creates a GitHub release. The package version comes from `version.json`, so set it to the tag's version before tagging.
