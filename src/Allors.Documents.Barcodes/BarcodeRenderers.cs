// <copyright file="BarcodeRenderers.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Barcodes
{
    using System.Collections.Generic;

    /// <summary>
    /// The barcode renderers at 300 dpi, for the render settings: <c>Renderers = [.. BarcodeRenderers.All]</c>. A renderer at another
    /// resolution is a new one: <c>new Code128Renderer { Dpi = 600 }</c>.
    /// </summary>
    public static class BarcodeRenderers
    {
        /// <summary>The <c>code128</c> renderer.</summary>
        public static readonly IPictureRenderer Code128 = new Code128Renderer();

        /// <summary>The <c>ean13</c> renderer.</summary>
        public static readonly IPictureRenderer Ean13 = new Ean13Renderer();

        /// <summary>The <c>qr</c> renderer.</summary>
        public static readonly IPictureRenderer Qr = new QrRenderer();

        /// <summary>The three renderers.</summary>
        public static readonly IReadOnlyList<IPictureRenderer> All = new[] { Code128, Ean13, Qr };
    }
}
