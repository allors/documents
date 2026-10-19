// <copyright file="BarcodeRendererTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Documents;
    using Allors.Documents.Barcodes;
    using SkiaSharp;
    using Xunit;
    using ZXing;
    using ZXing.Common;
    using static Allors.Documents.Tests.Render;

    /// <summary>
    /// The barcode renderers draw the value's text as a PNG of the frame's size in pixels at their dpi, with the quiet zone the symbology
    /// asks and without the text under the bars; a frame without a size takes the renderer's default size.
    /// </summary>
    public class BarcodeRendererTests
    {
        private static readonly PictureFrame Frame = new PictureFrame("Invoice.Number", 2.44, 0.32, PictureFit.Fill);
        private static readonly PictureFrame Unsized = new PictureFrame("Invoice.Number", 0, 0, PictureFit.Fill);
        private static readonly byte[] Placeholder = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly IDictionary<string, byte[]> Placeholders = new Dictionary<string, byte[]> { ["placeholder.png"] = Placeholder };

        [Fact]
        public void TheRenderersHaveTheirNamesAndAllListsThemInOrder()
        {
            Assert.Equal("code128", BarcodeRenderers.Code128.Name);
            Assert.Equal("ean13", BarcodeRenderers.Ean13.Name);
            Assert.Equal("qr", BarcodeRenderers.Qr.Name);
            Assert.Equal(["code128", "ean13", "qr"], BarcodeRenderers.All.Select(v => v.Name));
            Assert.Equal("code128", BarcodeRenderers.Code128.ToString());
            Assert.Equal(300, ((BarcodeRenderer)BarcodeRenderers.Code128).Dpi);
        }

        [Fact]
        public void TheBarcodeIsAPngOfTheFrameSizeInPixelsAtTheDpi()
        {
            var png = BarcodeRenderers.Code128.Render("INV-2026-001", "INV-2026-001", Frame);

            Assert.Same(PictureFormat.Png, PictureFormat.Sniff(png));
            Assert.Equal((732, 96), TestPictures.Size(png));
        }

        [Fact]
        public void TheDpiScalesThePixels()
        {
            var png = new Code128Renderer { Dpi = 600 }.Render("INV-2026-001", "INV-2026-001", Frame);

            Assert.Equal((1464, 192), TestPictures.Size(png));
        }

        [Fact]
        public void AFrameWithoutASizeTakesTheDefaultSize()
        {
            Assert.Equal((320, 80), TestPictures.Size(BarcodeRenderers.Code128.Render("INV-1", "INV-1", Unsized)));
            Assert.Equal((320, 80), TestPictures.Size(BarcodeRenderers.Ean13.Render("590123412345", "590123412345", Unsized)));
            Assert.Equal((320, 320), TestPictures.Size(BarcodeRenderers.Qr.Render("INV-1", "INV-1", Unsized)));
            Assert.Equal((320, 80), TestPictures.Size(BarcodeRenderers.Code128.Render("INV-1", "INV-1", null)));
        }

        [Fact]
        public void TheBarcodeReadsBackAsTheText()
        {
            Assert.Equal("INV-2026-001", ReadBack(BarcodeRenderers.Code128.Render("INV-2026-001", "INV-2026-001", Frame), BarcodeFormat.CODE_128));
            Assert.Equal("5901234123457", ReadBack(BarcodeRenderers.Ean13.Render("590123412345", "590123412345", Frame), BarcodeFormat.EAN_13));
            Assert.Equal("5901234123457", ReadBack(BarcodeRenderers.Ean13.Render("5901234123457", "5901234123457", Frame), BarcodeFormat.EAN_13));
            Assert.Equal("https://example.com/i/1", ReadBack(BarcodeRenderers.Qr.Render("https://example.com/i/1", "https://example.com/i/1", new PictureFrame("Qr", 1, 1, PictureFit.Fill)), BarcodeFormat.QR_CODE));
        }

        [Fact]
        public void TheBarsAreBlackOnWhiteWithAQuietZoneAndWithoutText()
        {
            var png = BarcodeRenderers.Code128.Render("INV-1", "INV-1", Frame);

            Assert.Equal(255, TestPictures.Grey(png, 0, 48));
            Assert.Equal(255, TestPictures.Grey(png, 731, 48));
            Assert.Equal(255, TestPictures.Grey(png, 366, 0));
            var middle = Enumerable.Range(0, 732).Select(x => TestPictures.Grey(png, x, 48)).ToList();
            Assert.Contains((byte)0, middle);
            Assert.DoesNotContain(middle, v => v != 0 && v != 255);
            Assert.Equal(middle, Enumerable.Range(0, 732).Select(x => TestPictures.Grey(png, x, 95)).ToList());
        }

        [Fact]
        public void EmptyTextGivesNoPicture()
        {
            Assert.Null(BarcodeRenderers.Code128.Render("", "", Frame));
            Assert.Null(BarcodeRenderers.Qr.Render(null, null, Frame));
        }

        [Fact]
        public void TextTheSymbologyCannotEncodeThrowsWithTheReason()
        {
            var ean = Assert.ThrowsAny<Exception>(() => BarcodeRenderers.Ean13.Render("abc", "abc", Frame));
            Assert.NotEmpty(ean.Message);
            Assert.ThrowsAny<Exception>(() => BarcodeRenderers.Ean13.Render("12345", "12345", Frame));
            Assert.ThrowsAny<Exception>(() => BarcodeRenderers.Code128.Render("café", "café", Frame));
        }

        [Fact]
        public void ATemplateDrawsTheBarcodeIntoTheFrameAtItsSize()
        {
            var settings = new RenderSettings { Renderers = [.. BarcodeRenderers.All] };
            var body = P(Odt.Frame("Barcode", "Pictures/placeholder.png", "<$Invoice.Number format=\"code128\">", size: "svg:width=\"2.44in\" svg:height=\"0.32in\""));
            var model = new Dictionary<string, object> { ["Invoice"] = new Dictionary<string, object> { ["Number"] = "INV-2026-001" } };

            var result = Document(body, model, settings, pictures: Placeholders);

            Assert.Empty(result.Findings);
            var png = Odt.Entry(result.Document, "Pictures/Invoice.Number.png").Bytes;
            Assert.Equal((732, 96), TestPictures.Size(png));
            Assert.Equal("INV-2026-001", ReadBack(png, BarcodeFormat.CODE_128));
            var frame = Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();
            Assert.Equal("2.44in", (string)frame.Attribute(Odt.Svg + "width"));
            Assert.Equal("0.32in", (string)frame.Attribute(Odt.Svg + "height"));
        }

        [Fact]
        public void ANumberIsEncodedAsItsTextUnderTheDefaults()
        {
            var settings = new RenderSettings { Renderers = [.. BarcodeRenderers.All], Culture = new System.Globalization.CultureInfo("nl-BE") };
            var body = P(Odt.Picture("<$Invoice.Id format=\"code128\">"));

            var result = Document(body, new Dictionary<string, object> { ["Invoice"] = new Dictionary<string, object> { ["Id"] = 12345 } }, settings, pictures: Placeholders);

            Assert.Empty(result.Findings);
            Assert.Equal("12345", ReadBack(Odt.Entry(result.Document, "Pictures/Invoice.Id.png").Bytes, BarcodeFormat.CODE_128));
        }

        [Fact]
        public void TextTheSymbologyCannotEncodeIsRendererFailedAndTheFrameHasNoPicture()
        {
            var settings = new RenderSettings { Renderers = [.. BarcodeRenderers.All] };
            var body = P(Odt.Picture("<$Invoice.Number format=\"ean13\">"));

            var result = Document(body, new Dictionary<string, object> { ["Invoice"] = new Dictionary<string, object> { ["Number"] = "INV-1" } }, settings, pictures: Placeholders);

            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.RendererFailed, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("<$Invoice.Number format=\"ean13\">", finding.Text);
            Assert.StartsWith("the renderer 'ean13' failed on 'Invoice.Number' and the frame has no picture: ", finding.Message);
            Assert.True(finding.Message.Length > "the renderer 'ean13' failed on 'Invoice.Number' and the frame has no picture: ".Length);
            Assert.Empty(Odt.PictureFiles(result.Document));
        }

        [Fact]
        public void ThePngWriterWritesTheMatrixAsGreyPixels()
        {
            var matrix = new BitMatrix(3, 2);
            matrix[0, 0] = true;
            matrix[2, 1] = true;

            var png = PngWriter.Write(matrix);

            Assert.Same(PictureFormat.Png, PictureFormat.Sniff(png));
            Assert.Equal((3, 2), TestPictures.Size(png));
            Assert.Equal(new byte[] { 0, 255, 255, 255, 255, 0 }, new[] { (0, 0), (1, 0), (2, 0), (0, 1), (1, 1), (2, 1) }.Select(v => TestPictures.Grey(png, v.Item1, v.Item2)));
        }

        /// <summary>Reads the barcode back from the PNG's pixels with ZXing, as a scanner would.</summary>
        private static string ReadBack(byte[] png, BarcodeFormat format)
        {
            using (var bitmap = SKBitmap.Decode(png))
            {
                var grey = new byte[bitmap.Width * bitmap.Height];
                for (var y = 0; y < bitmap.Height; y++)
                {
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        grey[y * bitmap.Width + x] = bitmap.GetPixel(x, y).Red;
                    }
                }

                var reader = new BarcodeReaderGeneric { Options = { PossibleFormats = new[] { format }, PureBarcode = true } };
                var result = reader.Decode(new RGBLuminanceSource(grey, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8));
                return result?.Text;
            }
        }
    }
}
