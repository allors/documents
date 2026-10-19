// <copyright file="IPictureRenderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>A renderer a picture tag may name: it gives the picture the frame shows for its value, such as a barcode for a number.</summary>
    public interface IPictureRenderer : IRenderer
    {
        /// <summary>
        /// The bytes of the picture a frame shows for a value, in a format the library knows, drawn for the frame's size and fit. The text is the
        /// value under the library's defaults, the text a map would look up: <c>12345</c> for a number whatever the culture, the name of an enum,
        /// text as it is; a renderer that encodes text uses it and never meets the value's type. Null when the value gives no picture, in which case
        /// the frame leaves the document or keeps the template's picture, as its empty attribute says. It is never called for a null value.
        /// An exception, or bytes in no format the library knows, is reported as RendererFailed and the frame has no picture.
        /// </summary>
        byte[] Render(object value, string text, PictureFrame frame);
    }
}
