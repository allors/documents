---
title: "Pictures"
description: "Binding a picture frame through its description: the picture tag, the empty and fit attributes, the formats the library knows and how pictures are stored."
audience: "Template authors"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for template authors.

This page is the reference for bound pictures: the picture tag in a frame's description, what the
frame does without a picture, how a picture takes its frame and how pictures are stored.
[Write a template](write-a-template.md#bind-a-picture) has the Writer steps.

A picture binds through its description (Image, Properties, Options, Description; LibreOffice's Alt Text dialog labels the same box Alt Text), which holds exactly one picture tag:

```
<$Issuer.Logo>
<$item.Photo fit="contain" empty="keep">
```

The frame shows the picture at that path: the bytes the resolver yields for the value, or the picture a renderer draws for it when the tag names one in `format`, in a format the bytes tell, PNG, JPEG, GIF, WebP, BMP, TIFF, SVG, EMF or WMF. The frame's name stays the author's and its alternative text stays in the output; the description that held the tag leaves it.

| Attribute | Meaning |
|---|---|
| `empty="remove"` | The default: without a picture the frame leaves the document. A null value leaves in silence; a member the model does not have, or a value that is no picture, leaves with a finding. |
| `empty="keep"` | Without a picture the frame keeps the template's picture, as the author drew it. |
| `fit="fill"` | The default: the frame stays as drawn and the picture is stretched into it. What a barcode needs. |
| `fit="contain"` | The picture keeps its proportions and is as large as fits inside the frame: the side that would overflow shrinks. |
| `fit="width"` | The frame keeps its width; its height follows the picture's proportions, and may grow. |
| `fit="height"` | The frame keeps its height; its width follows the picture's proportions, and may grow. |
| `format="code128"` | The picture renderer that draws the frame's picture for the value, by name, instead of the picture the resolver would give: a barcode for a number, for instance. The renderer gets the value, its text under the library's defaults and the frame's size and fit; the frame keeps its size unless `fit` says otherwise. A name that is no picture renderer is a `BadValue` finding, and the frame shows the resolver's picture. [Renderers and picture processing](renderers.md) says how to register one. |

The proportions come from the picture's header. Bytes that do not tell their size, and a frame without a width and a height, fill and report `CannotFit`.

A picture goes through the picture processor of the render settings, when there is one, before it is stored: the processor may scale it to the frame or convert it, and a processor that fails is reported as `RendererFailed` and the picture is stored as it was.

A picture is stored once per distinct content, under `Pictures/<path>.<ext>`, with `.2`, `.3` for further pictures at the same path: `Pictures/Issuer.Logo.png`, `Pictures/item.Photo.jpg`, `Pictures/item.Photo.2.jpg`. Template pictures that no frame shows any more leave the package and its manifest. Every frame's name is made unique in the document (`Photo`, `Photo 2`), as Writer requires, so a bound frame may sit in a loop.
