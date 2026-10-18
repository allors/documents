---
title: "Pictures"
description: "Binding a picture frame through its description: the picture tag, the empty and fit attributes, the formats the library knows and how pictures are stored."
audience: "Template authors"
kind: "Reference"
status: "Current"
---

> **Status: Current.** Reference for template authors.

A picture binds through its description (Image, Properties, Options, Description), which holds exactly one picture tag:

```
<$Issuer.Logo>
<$item.Photo fit="contain" empty="keep">
```

The frame shows the picture at that path: the bytes the resolver yields for the value, in a format the bytes tell, PNG, JPEG, GIF, WebP, BMP, TIFF, SVG, EMF or WMF. The frame's name stays the author's and its alternative text stays in the output; the description that held the tag leaves it.

| Attribute | Meaning |
|---|---|
| `empty="remove"` | The default: without a picture the frame leaves the document. A null value leaves in silence; a member the model does not have, or a value that is no picture, leaves with a finding. |
| `empty="keep"` | Without a picture the frame keeps the template's picture, as the author drew it. |
| `fit="fill"` | The default: the frame stays as drawn and the picture is stretched into it. What a barcode needs. |
| `fit="contain"` | The picture keeps its proportions and is as large as fits inside the frame: the side that would overflow shrinks. |
| `fit="width"` | The frame keeps its width; its height follows the picture's proportions, and may grow. |
| `fit="height"` | The frame keeps its height; its width follows the picture's proportions, and may grow. |

The proportions come from the picture's header. Bytes that do not tell their size, and a frame without a width and a height, fill and report `CannotFit`.

A picture is stored once per distinct content, under `Pictures/<path>.<ext>`, with `.2`, `.3` for further pictures at the same path: `Pictures/Issuer.Logo.png`, `Pictures/item.Photo.jpg`, `Pictures/item.Photo.2.jpg`. Template pictures that no frame shows any more leave the package and its manifest. Every frame's name is made unique in the document (`Photo`, `Photo 2`), as Writer requires, so a bound frame may sit in a loop.
