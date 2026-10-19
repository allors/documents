---
title: "Renderers and picture processing"
description: "Register renderers that transform one value by name, such as upper-case text or a barcode, and a picture processor that prepares pictures for their frames; the barcode and imaging packages are planned."
audience: "Developers"
kind: "How-to guide"
status: "Planned"
---

> **Status: Planned.** How-to guide for developers. The renderer contracts, the built-in text
> renderers, the `format` selection and the picture processor below are built for version 4.0.0.
> The packages `Allors.Documents.Barcodes` and `Allors.Documents.Imaging` are agreed and not built
> yet; their section says so.

A renderer is Parr's attribute renderer, as the [Design](design.md#model-view-controller-and-renderer)
page explains it: a display transformation of one value, configured by the controller and
selected by the view. The library carries two extension points, both set in the
[render settings](api.md#render-settings): the renderers a tag selects by name, and a picture
processor that sees every bound picture before it is stored. Both keep the core free of
third-party dependencies; the capabilities that need one come as packages.

## Know what a renderer may do

A renderer encodes, formats or draws the one value it is given: text in upper case, a barcode
from a number, plain text from markdown. It never reads a second value, compares or decides.
Coalescing two members, grouping lines, summing amounts and choosing a clause by a condition
belong to the model or to the virtual members of a [resolver](resolve-your-own-model.md). Nothing
enforces this rule; it is the line the design draws for the author of a renderer.

A renderer is shared by every render of a template, from any thread, so it keeps no state of a
render.

## Select a renderer in a tag

A tag names a renderer in its `format` attribute, in any case: `<$Customer.Name format="upper">`
on a binding, `<$Invoice.Number format="code128">` in the description of a picture frame. The
name is looked up before the pattern names `date`, `time`, `datetime` and `number` and before a
.NET format string, so a registered name wins over both. The library ships the text renderers
`upper`, `lower` and `cap`, which transform the formatted text in the culture of the formatting;
every template has them.

A text renderer, `ITextRenderer`, receives the value as the resolver read it and the text the
resolver formatted it to under the effective formatting, whose `Format` is null because the
renderer's name took its place, and gives the text the binding prints. A number with
`format="upper"` prints with the document's number pattern, then in upper case. A `map` wins over
the renderer, and `blank` applies to what the renderer gives.

A picture renderer, `IPictureRenderer`, receives the value, its text under the library's
defaults, which is the text a [map](maps.md) would look up, `12345` for a number whatever the
culture, and the frame, a `PictureFrame` with its size in inches as the author drew it and the
tag's `fit`. It gives the picture's bytes in a format the library knows, or null for no picture,
in which case the frame does what its `empty` attribute says. The frame shows the renderer's
picture for a value of any kind: a number, text, a date.

A renderer that is both serves a binding and a picture frame under one name.

## Register renderers

The caller lists its renderers in the render settings. The template reads them once, after the
built-in ones; a later renderer replaces an earlier one with its name, built in or not, and keeps
its place. A null renderer, or one without a name, throws `ArgumentException` when the template is
read.

```csharp
using Allors.Documents;

public sealed class WordsRenderer : ITextRenderer
{
    public string Name => "words";

    public string Render(object value, string text, Formatting formatting) =>
        value is int number && number < 10 ? Words[number] : text;
}

var settings = new RenderSettings
{
    Renderers = [new WordsRenderer()],
};
var template = new OpenDocumentTemplate(templateBytes, settings);
```

`Validate` consults the same renderers: a `format` that names no renderer and is no pattern is a
`BadPattern` error whose message lists the text renderers; a picture renderer on a binding, and
a text renderer or an unknown name on a picture tag, are `BadValue` errors. A text renderer passes
on a value of any kind, where a pattern on text would be a `FormatIgnored` warning. At render
time a renderer that throws, or a picture renderer that gives bytes in no format the library
knows, is reported as `RendererFailed`: the binding prints blank, or the frame has no picture.
[Findings](findings.md) lists them.

## Process pictures before they are stored

A picture processor, `IPictureProcessor`, is the step between a picture's bytes and the package:
it receives the bytes the model or a renderer gave, their `PictureFormat` and the `PictureFrame`,
and returns the bytes to store, in the same format or another the library knows; the result is
sniffed again. The same bytes, or null, store the picture as it was. One processor serves every
bound picture of every render of the template. It is set in the render settings, not on the
resolver, because scaling a picture to its frame is presentation geometry and resolving is not.
A processor that throws, or gives bytes in no format the library knows, is reported as a
`RendererFailed` warning and the picture is stored as it was.

```csharp
var settings = new RenderSettings
{
    PictureProcessor = new PictureScaling(),
};
```

## Packages

> **Planned.** The packages below are agreed and not built yet.

| Package | Depends on | Gives |
| --- | --- | --- |
| `Allors.Documents.Barcodes` | ZXing.Net | The picture renderers `code128`, `ean13` and `qr`: a barcode drawn from the value's text, sized for its frame. |
| `Allors.Documents.Imaging` | SkiaSharp | A picture processor that scales a picture down to its frame at a resolution the caller chooses, never up, applies the EXIF orientation and converts between formats. |

Each package carries its own dependency and its own release notes. The core package stays
without dependencies.
