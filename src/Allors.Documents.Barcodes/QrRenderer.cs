// <copyright file="QrRenderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Barcodes
{
    using ZXing;

    /// <summary>The <c>qr</c> renderer: a QR code of the text, drawn square in the middle of the frame; a square frame shows it whole.</summary>
    public sealed class QrRenderer : BarcodeRenderer
    {
        public override string Name => "qr";

        protected override BarcodeFormat Format => BarcodeFormat.QR_CODE;

        /// <summary>A square, as a QR code is.</summary>
        protected override (int Width, int Height) DefaultSize => (320, 320);
    }
}
