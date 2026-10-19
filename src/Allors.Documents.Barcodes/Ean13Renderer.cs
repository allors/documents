// <copyright file="Ean13Renderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Barcodes
{
    using ZXing;

    /// <summary>The <c>ean13</c> renderer: an EAN-13 barcode of 12 digits, whose check digit it computes, or of 13 digits, whose check digit it verifies.</summary>
    public sealed class Ean13Renderer : BarcodeRenderer
    {
        public override string Name => "ean13";

        protected override BarcodeFormat Format => BarcodeFormat.EAN_13;
    }
}
