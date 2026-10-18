// <copyright file="IPictureProcessor.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Models
{
    using Allors.Document.OpenDocument;

    /// <summary>
    /// The step between a picture's bytes and the package: a processor sees the bytes the model or a renderer gave for a frame, their format and
    /// the frame, and gives the bytes the package stores, such as the picture scaled to the frame. One processor serves every bound picture of a
    /// render; the caller sets it in the render settings.
    /// </summary>
    public interface IPictureProcessor
    {
        /// <summary>
        /// The bytes to store for the picture: new bytes in a format the library knows, or the same bytes or null to store them as they are.
        /// The result is sniffed again, so a processor may change the format. An exception, or bytes in no format the library knows, is reported
        /// as RendererFailed and the original bytes are stored.
        /// </summary>
        byte[] Process(byte[] bytes, PictureFormat format, PictureFrame frame);
    }
}
