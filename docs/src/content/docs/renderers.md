---
title: "Renderers and picture processing"
description: "Register renderers that transform one value by name, such as upper-case text or a barcode, and a picture processor that prepares pictures for their frames: the agreed design for version 4.0, not yet built."
audience: "Developers"
kind: "How-to guide"
status: "Planned"
---

> **Status: Planned.** How-to guide for developers. The design below was agreed on 2026-10-18 for
> version 4.0.0. Nothing of it is on `main` or in a released version, and the names and
> signatures can change before it lands. The tags and attributes in [Tags](tags.md),
> [Formatting](formatting.md) and [Pictures](pictures.md) are what renders today.

A renderer is Parr's attribute renderer, as the [Design](design.md#model-view-controller-and-renderer)
page explains it: a display transformation of one value, configured by the controller and
selected by the view. The library will carry two extension points, both set in the render
settings: a registry of renderers that a tag selects by name, and a picture processor that sees
every picture before it is stored. Both keep the core free of third-party dependencies; the
capabilities that need one come as packages.

## Know what a renderer may do

A renderer encodes, formats or shrinks the one value it is given: text in upper case, a barcode
from a number, plain text from markdown, a date in a pattern. It never reads a second value,
compares or decides. Coalescing two members, grouping lines, summing amounts and choosing a
clause by a condition belong to the model or to the virtual members of a
[resolver](resolve-your-own-model.md). Nothing enforces this rule; it is the line the design
draws for the author of a renderer.

## Select a renderer in a tag

A tag names a renderer in its `format` attribute, in any case: `<$Customer.Name format="upper">`
on a binding, `<$Invoice.Number format="code128">` in the description of a picture frame. A
registered name wins over the pattern names `date`, `time`, `datetime` and `number` and over a
.NET format string. A text renderer receives the value and the text the resolver formatted it to,
and gives the text the binding prints. A picture renderer receives the value and the frame, its
size as the author drew it and its `fit`, and gives the picture's bytes in a format the library
knows. The library ships the text renderers `upper`, `lower` and `cap`, which transform the
formatted text in the culture of the formatting.

Whether `format` also selects picture renderers, or pictures get an attribute of their own, is an
open point of the design.

## Register renderers

The caller registers renderers in the render settings, one at a time or as a collection, and
passes the same registry to `Validate`. A renderer added under a name the registry has replaces
the one it had, built in or not.

```csharp
// The agreed design with provisional names, not a released API.
var settings = new RenderSettings
{
    Renderers = { new BarcodeRenderers(), new PlainTextRenderer() },
    PictureProcessor = new PictureScaling(dpi: 300),
};
```

`Validate` consults the registry: a `format` that names no renderer and is no pattern is an
error, and so are a picture renderer on a binding and a text renderer on a picture. At render
time a renderer that throws, or a picture renderer that gives bytes in no format the library
knows, is reported as a finding of its own kind: the binding prints blank, or the frame has no
picture and does what its `empty` attribute says.

## Process pictures before they are stored

A picture processor is the step between a picture's bytes and the package: it receives the bytes
the model or a renderer gave, their format and the frame, and returns the bytes to store, in the
same format or another the library knows. One processor serves every bound picture of a render.
It is set in the render settings, not on the resolver, because scaling a picture to its frame is
presentation geometry and resolving is not. A processor that throws is reported as a warning and
the picture is stored as it was.

## Packages

| Package | Depends on | Gives |
| --- | --- | --- |
| `Allors.Documents.Barcodes` | ZXing.Net | The picture renderers `code128`, `ean13` and `qr`: a barcode drawn from the value's text, sized for its frame. |
| `Allors.Documents.Imaging` | SkiaSharp | A picture processor that scales a picture down to its frame at a resolution the caller chooses, never up, applies the EXIF orientation and converts between formats. |

Each package carries its own dependency and its own release notes. The core package stays
without dependencies.

## Open points

- Whether `format` selects picture renderers, or pictures get an attribute of their own.
- The shape of the renderer interfaces, and whether a renderer declares the kinds of value it serves.
- The names of the registry, of the built-in renderers' types and of the packages' classes.

They are decided with the 4.0.0 API. This page then describes what landed and becomes Current.
