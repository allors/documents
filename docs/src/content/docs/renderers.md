---
title: "Renderers and picture processing"
description: "Register renderers that transform one value by name, such as upper-case text or a barcode, and a picture processor that prepares pictures for their frames, by wrapping your own code or with the optional barcode and imaging packages."
audience: "Developers"
kind: "How-to guide"
status: "Current"
---

> **Status: Current.** How-to guide for developers.

A renderer is Parr's attribute renderer, as the [Design](design.md#model-view-controller-and-renderer)
page explains it: a display transformation of one value, configured by the controller and
selected by the view. The library carries two extension points, both set in the
[render settings](api.md#render-settings): the renderers a tag selects by name, and a picture
processor that sees every bound picture before it is stored. Both are contracts of the core
package, which an application implements with the code it has: a barcode service it already
runs, the image scaling its uploads go through. The [packages](#packages) `Allors.Documents.Barcodes`
and `Allors.Documents.Imaging` are optional implementations of those contracts for an application
that has neither; an application that has the capability wraps its own, so that documents and
the rest of the application draw the same barcode and scale the same way, with no second
dependency.

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

## Write a picture renderer

A picture renderer of your own wraps what the application draws already. It gets the text to
encode and the frame to draw for, and gives the bytes of a picture in a format the library
knows; the library sizes the frame to them by the tag's `fit`. A barcode service the application
runs becomes a renderer in a few lines, and the invoice and the shipping label then carry the
same barcode:

```csharp
using System;
using Allors.Documents;

public sealed class Code128Renderer : IPictureRenderer
{
    private readonly IBarcodeService barcodes;

    public Code128Renderer(IBarcodeService barcodes) => this.barcodes = barcodes;

    public string Name => "code128";

    public byte[] Render(object value, string text, PictureFrame frame)
    {
        // The frame's inches at 300 dpi; the service's own default when the frame has no size.
        var width = frame.HasSize ? (int)Math.Round(frame.Width * 300) : 320;
        var height = frame.HasSize ? (int)Math.Round(frame.Height * 300) : 80;
        return this.barcodes.Code128(text, width, height); // a PNG
    }
}

var settings = new RenderSettings
{
    Renderers = [new Code128Renderer(barcodes)],
};
```

The renderer takes `text`, not `value`: the text is the value under the library's defaults, so a
number encodes as `12345` whatever the culture. Return null for a value that gives no picture,
and let an exception through for text the service cannot encode: the render reports it as
`RendererFailed` with the message and goes on.

## Process pictures before they are stored

A picture processor, `IPictureProcessor`, is the step between a picture's bytes and the package:
it receives the bytes the model or a renderer gave, their `PictureFormat` and the `PictureFrame`,
and returns the bytes to store, in the same format or another the library knows; the result is
sniffed again. The same bytes, or null, store the picture as it was. One processor serves every
bound picture of every render of the template. It is set in the render settings, not on the
resolver, because scaling a picture to its frame is presentation geometry and resolving is not.
A processor that throws, or gives bytes in no format the library knows, is reported as a
`RendererFailed` warning and the picture is stored as it was.

A processor of your own wraps the scaling the application does already, for its uploads or its
thumbnails, so that a photo prints the way the application shows it. The frame gives the inches
to scale for and the fit; the processor decides the resolution and the format:

```csharp
using System;
using Allors.Documents;

public sealed class PhotoScaling : IPictureProcessor
{
    public byte[] Process(byte[] bytes, PictureFormat format, PictureFrame frame)
    {
        if (format != PictureFormat.Jpeg || !frame.HasSize)
        {
            return bytes; // stored as it is
        }

        var maxWidth = (int)Math.Round(frame.Width * 300);
        return Photos.Resize(bytes, maxWidth); // the application's own scaling, never up
    }
}

var settings = new RenderSettings
{
    PictureProcessor = new PhotoScaling(),
};
```

## Packages

The two packages below are optional. They implement the contracts above for an application that
draws no barcodes and scales no pictures of its own; an application that does wraps its own code
as the sections above show, and needs neither package. Each package carries its dependency; the
core package stays without.

| Package | Depends on | Gives |
| --- | --- | --- |
| `Allors.Documents.Barcodes` | ZXing.Net | The picture renderers `code128`, `ean13` and `qr`: a barcode of the value's text, drawn for the frame that shows it. |
| `Allors.Documents.Imaging` | SkiaSharp | `PictureScaling`, a picture processor that scales a picture down to its frame, turns a photo upright and keeps its format. |

### Draw barcodes

`BarcodeRenderers.All` lists the three renderers, in the namespace `Allors.Documents.Barcodes`;
`BarcodeRenderers.Code128`, `Ean13` and `Qr` are the same one by one. A renderer at another
resolution is a new one: `new Code128Renderer { Dpi = 600 }`.

```csharp
using Allors.Documents.Barcodes;

var settings = new RenderSettings
{
    Renderers = [.. BarcodeRenderers.All],
};
```

In the template, the picture's description names the renderer:
`<$Invoice.Number format="code128" empty="remove">`.

A barcode is a PNG, black bars on white, of the frame's size in pixels at the renderer's `Dpi`,
300 unless set: a frame of 2.44 by 0.32 inches gives 732 by 96 pixels. The frame's `fit` is best
left `fill`, the default, so that the frame stays as the author drew it. A frame without a size,
one drawn relative to its paragraph, gets 320 by 80 pixels, and `qr` 320 by 320. The quiet zone
the symbology asks is part of the picture; the text under the bars is not, so a binding below the
frame prints the number. The frame shows a value of any kind, a number as its text under the
library's defaults, `12345` whatever the culture.

| Renderer | Encodes |
| --- | --- |
| `code128` | Any printable ASCII text. |
| `ean13` | 12 digits, whose check digit it computes, or 13 digits, whose check digit it verifies. |
| `qr` | Any text, as a square in the middle of the frame; a square frame shows it whole. |

Text a symbology cannot encode, letters for `ean13`, is reported as `RendererFailed` with ZXing's
reason, and the frame has no picture. Empty text gives no picture, as a null value does, and the
frame does what its `empty` attribute says.

### Scale pictures to their frames

`PictureScaling`, in the namespace `Allors.Documents.Imaging`, scales a bound picture down to the
pixels its frame needs at its `Dpi`, 300 unless set: a camera photo of 4000 pixels in a frame of
3.96 inches adds megabytes and no detail. A frame's inches times the dpi are the pixels its sides
need, and the tag's `fit` says which side counts: `fill` the side that needs the most, `contain`
the side that fits first, `width` and `height` their own side; a side without a length is left
out. A picture of 1600 by 1200 pixels in a frame of 3.96 by 2.97 inches with `fit="contain"`
becomes 1188 by 891 pixels. A picture is never scaled up: one of 500 by 400 pixels in a frame of
1.92 by 1.44 inches is stored as it is.

A picture that fits its frame and is upright passes through byte for byte, without being
decoded. A photo that carries an orientation, a portrait taken with a phone, is decoded and its
pixels turned the way the orientation says, so that the document shows it upright and the frame
fits to the upright sides. A PNG stays a PNG, a picture with transparency is stored as PNG, and
every other picture as JPEG at `JpegQuality`, 85 unless set. A result that would not be smaller
than the original leaves the original as it was. An SVG, EMF, WMF or TIFF, and bytes SkiaSharp
cannot decode, pass through.

```csharp
using Allors.Documents.Imaging;

var settings = new RenderSettings
{
    PictureProcessor = new PictureScaling { Dpi = 300, JpegQuality = 85 },
};
```

The package references SkiaSharp, which brings its native library for Windows and macOS. An
application on Linux adds `SkiaSharp.NativeAssets.Linux` of the same version, as the test project
of this repository does for its continuous integration.
