// <copyright file="BarcodeRenderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Barcodes
{
    using System;
    using ZXing;

    /// <summary>
    /// A picture renderer that draws a barcode of the value's text into the frame: <c>&lt;$Invoice.Number format="code128"&gt;</c> in the
    /// description of a picture. The barcode is a PNG of the frame's size in pixels at <see cref="Dpi"/>, black bars on white with the quiet
    /// zone the symbology asks, without the text under the bars; a binding below the frame prints the number. A frame without a size takes
    /// the renderer's default size. Text the symbology cannot encode, such as letters for ean13, is reported as RendererFailed with the reason.
    /// A renderer is shared by every render, from any thread.
    /// </summary>
    public abstract class BarcodeRenderer : IPictureRenderer
    {
        /// <summary>The resolution at which the frame's inches become pixels: 2.44 inches at 300 dpi are 732 pixels.</summary>
        public int Dpi { get; init; } = 300;

        public abstract string Name { get; }

        /// <summary>The symbology ZXing draws for the renderer.</summary>
        protected abstract BarcodeFormat Format { get; }

        /// <summary>The size in pixels when the frame has none; 320 by 80 for a one-dimensional code.</summary>
        protected virtual (int Width, int Height) DefaultSize => (320, 80);

        public byte[] Render(object value, string text, PictureFrame frame)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var size = frame != null && frame.HasSize ? (Width: Pixels(frame.Width), Height: Pixels(frame.Height)) : this.DefaultSize;
            var matrix = new MultiFormatWriter().encode(text, this.Format, size.Width, size.Height, null);
            return PngWriter.Write(matrix);
        }

        public override string ToString() => this.Name;

        private int Pixels(double inches) => Math.Max(1, (int)Math.Round(inches * this.Dpi));
    }
}
