// <copyright file="Code128Renderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Barcodes
{
    using ZXing;

    /// <summary>The <c>code128</c> renderer: a Code 128 barcode of the text, which may hold any printable ASCII character.</summary>
    public sealed class Code128Renderer : BarcodeRenderer
    {
        public override string Name => "code128";

        protected override BarcodeFormat Format => BarcodeFormat.CODE_128;
    }
}
