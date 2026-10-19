// <copyright file="PictureScalingTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Allors.Documents;
    using Allors.Documents.Imaging;
    using SkiaSharp;
    using Xunit;
    using static Allors.Documents.Tests.Render;

    /// <summary>
    /// The picture scaling processor scales a bound picture down to the pixels its frame needs at the dpi, by the tag's fit, never up;
    /// it turns a photo upright, keeps a PNG a PNG and writes the rest as JPEG, and passes through what it cannot or need not change.
    /// </summary>
    public class PictureScalingTests
    {
        private static readonly PictureScaling Scaling = new PictureScaling();
        private static readonly byte[] Placeholder = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly IDictionary<string, byte[]> Placeholders = new Dictionary<string, byte[]> { ["placeholder.png"] = Placeholder };

        [Fact]
        public void APictureThatFitsItsFrameUprightPassesThroughByteForByte()
        {
            var png = TestPictures.Png(500, 400);

            Assert.Same(png, Scaling.Process(png, PictureFormat.Png, Frame(1.92, 1.44, PictureFit.Contain)));
            Assert.Same(png, Scaling.Process(png, PictureFormat.Png, Frame(10, 10, PictureFit.Fill)));
            Assert.Same(png, Scaling.Process(png, PictureFormat.Png, Frame(0, 0, PictureFit.Contain)));
            Assert.Same(png, Scaling.Process(png, PictureFormat.Png, null));
        }

        [Fact]
        public void ContainScalesToTheSideThatFitsFirst()
        {
            var png = Scaling.Process(TestPictures.Png(1600, 1200), PictureFormat.Png, Frame(3.96, 2.97, PictureFit.Contain));
            var jpeg = Scaling.Process(TestPictures.Jpeg(3713, 2475), PictureFormat.Jpeg, Frame(3.96, 2.97, PictureFit.Contain));

            Assert.Same(PictureFormat.Png, PictureFormat.Sniff(png));
            Assert.Equal((1188, 891), TestPictures.Size(png));
            Assert.Same(PictureFormat.Jpeg, PictureFormat.Sniff(jpeg));
            Assert.Equal((1188, 792), TestPictures.Size(jpeg));
        }

        [Fact]
        public void FillScalesToTheSideThatNeedsMostAndContainToTheOther()
        {
            var picture = TestPictures.Png(1600, 1200);

            Assert.Equal((600, 450), TestPictures.Size(Scaling.Process(picture, PictureFormat.Png, Frame(2, 0.5, PictureFit.Fill))));
            Assert.Equal((200, 150), TestPictures.Size(Scaling.Process(picture, PictureFormat.Png, Frame(2, 0.5, PictureFit.Contain))));
        }

        [Fact]
        public void WidthAndHeightUseTheirOwnSide()
        {
            var picture = TestPictures.Png(1600, 1200);

            Assert.Equal((600, 450), TestPictures.Size(Scaling.Process(picture, PictureFormat.Png, Frame(2, 0.5, PictureFit.Width))));
            Assert.Equal((200, 150), TestPictures.Size(Scaling.Process(picture, PictureFormat.Png, Frame(2, 0.5, PictureFit.Height))));
            Assert.Same(picture, Scaling.Process(picture, PictureFormat.Png, Frame(0, 0.5, PictureFit.Width)));
            Assert.Equal((200, 150), TestPictures.Size(Scaling.Process(picture, PictureFormat.Png, Frame(0, 0.5, PictureFit.Contain))));
        }

        [Fact]
        public void TheDpiSetsThePixelsTheFrameNeeds()
        {
            var scaling = new PictureScaling { Dpi = 150 };

            var png = scaling.Process(TestPictures.Png(1600, 1200), PictureFormat.Png, Frame(3.96, 2.97, PictureFit.Contain));

            Assert.Equal((594, 446), TestPictures.Size(png));
            Assert.Equal("PictureScaling 150 dpi", scaling.ToString());
        }

        [Fact]
        public void TheJpegQualitySetsTheSizeOfAJpeg()
        {
            var photo = TestPictures.Jpeg(1600, 1200);
            var frame = Frame(2, 1.5, PictureFit.Contain);

            var fine = new PictureScaling { JpegQuality = 95 }.Process(photo, PictureFormat.Jpeg, frame);
            var coarse = new PictureScaling { JpegQuality = 20 }.Process(photo, PictureFormat.Jpeg, frame);

            Assert.Equal((600, 450), TestPictures.Size(fine));
            Assert.Equal((600, 450), TestPictures.Size(coarse));
            Assert.True(coarse.Length < fine.Length);
        }

        [Fact]
        public void APngStaysAPngAndTransparencyIsKeptAsPngAndTheRestBecomesJpeg()
        {
            var frame = Frame(1, 1, PictureFit.Contain);

            Assert.Same(PictureFormat.Png, PictureFormat.Sniff(Scaling.Process(TestPictures.Png(800, 800), PictureFormat.Png, frame)));
            Assert.Same(PictureFormat.Png, PictureFormat.Sniff(Scaling.Process(TestPictures.Png(800, 800, transparent: true), PictureFormat.Png, frame)));
            Assert.Same(PictureFormat.Png, PictureFormat.Sniff(Scaling.Process(TestPictures.WebP(800, 800, transparent: true), PictureFormat.WebP, frame)));
            Assert.Same(PictureFormat.Jpeg, PictureFormat.Sniff(Scaling.Process(TestPictures.Jpeg(800, 800), PictureFormat.Jpeg, frame)));
            var bmp = TestPictures.Bmp(800, 800);
            Assert.Same(PictureFormat.Bmp, PictureFormat.Sniff(bmp));
            var fromBmp = Scaling.Process(bmp, PictureFormat.Bmp, frame);
            Assert.Same(PictureFormat.Jpeg, PictureFormat.Sniff(fromBmp));
            Assert.Equal((300, 300), TestPictures.Size(fromBmp));
            Assert.True(TestPictures.IsRed(fromBmp, 50, 50));
            Assert.False(TestPictures.IsRed(fromBmp, 250, 250));
        }

        [Fact]
        public void AScaledPictureThatWouldNotBeSmallerStaysAsItWas()
        {
            var flat = TestPictures.WebP(800, 800);
            var frame = Frame(1, 1, PictureFit.Contain);

            var result = Scaling.Process(flat, PictureFormat.WebP, frame);

            Assert.Same(flat, result);
        }

        [Fact]
        public void TheOrientationIsAppliedToThePixelsOfAPhotoThatFits()
        {
            var photo = TestPictures.WithOrientation(TestPictures.Jpeg(400, 200), 6);
            Assert.Equal(SKEncodedOrigin.RightTop, TestPictures.Origin(photo));
            var frame = Frame(10, 10, PictureFit.Contain);

            var upright = Scaling.Process(photo, PictureFormat.Jpeg, frame);

            Assert.NotSame(photo, upright);
            Assert.Equal((200, 400), TestPictures.Size(upright));
            Assert.Equal(SKEncodedOrigin.TopLeft, TestPictures.Origin(upright));
            Assert.True(TestPictures.IsRed(upright, 150, 100));
            Assert.False(TestPictures.IsRed(upright, 50, 100));
            Assert.False(TestPictures.IsRed(upright, 150, 300));

            var turned = Scaling.Process(TestPictures.WithOrientation(TestPictures.Jpeg(400, 200), 3), PictureFormat.Jpeg, frame);

            Assert.Equal((400, 200), TestPictures.Size(turned));
            Assert.True(TestPictures.IsRed(turned, 350, 150));
            Assert.False(TestPictures.IsRed(turned, 50, 50));
        }

        [Fact]
        public void AnUprightPhotoWithAnOrientationTagPassesThrough()
        {
            var photo = TestPictures.WithOrientation(TestPictures.Jpeg(400, 200), 1);

            Assert.Same(photo, Scaling.Process(photo, PictureFormat.Jpeg, Frame(10, 10, PictureFit.Contain)));
        }

        [Fact]
        public void ATurnedPhotoIsScaledByItsDisplayedSides()
        {
            var photo = TestPictures.WithOrientation(TestPictures.Jpeg(1600, 1200), 6);

            var result = Scaling.Process(photo, PictureFormat.Jpeg, Frame(2, 3, PictureFit.Contain));

            Assert.Equal((600, 800), TestPictures.Size(result));
            Assert.Equal(SKEncodedOrigin.TopLeft, TestPictures.Origin(result));
        }

        [Fact]
        public void WhatCannotBeDecodedOrNeedNotBePassesThrough()
        {
            var frame = Frame(1, 1, PictureFit.Contain);
            var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"4000\" height=\"4000\"/>");
            var tiff = new byte[] { 0x49, 0x49, 0x2A, 0x00, 8, 0, 0, 0, 0, 0 };
            var garbage = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8 };
            var header = FitTests.Png(4000, 4000);

            Assert.Same(svg, Scaling.Process(svg, PictureFormat.Svg, frame));
            Assert.Same(tiff, Scaling.Process(tiff, PictureFormat.Tiff, frame));
            Assert.Same(garbage, Scaling.Process(garbage, PictureFormat.Png, frame));
            Assert.Same(header, Scaling.Process(header, PictureFormat.Png, frame));
            Assert.Null(Scaling.Process(null, PictureFormat.Png, frame));
            Assert.Empty(Scaling.Process(new byte[0], PictureFormat.Png, frame));
        }

        [Fact]
        public void ATemplateStoresTheScaledPictureAndFitsTheFrameToIt()
        {
            var settings = new RenderSettings { PictureProcessor = Scaling };
            var body = P(Odt.Frame("Photo", "Pictures/placeholder.png", "<$Part.Photo fit=\"contain\">", size: "svg:width=\"2in\" svg:height=\"1.5in\""));
            var model = new Dictionary<string, object> { ["Part"] = new Dictionary<string, object> { ["Photo"] = TestPictures.Jpeg(1600, 1200) } };

            var result = Document(body, model, settings, pictures: Placeholders);

            Assert.Empty(result.Findings);
            Assert.Equal(["Pictures/Part.Photo.jpg"], Odt.PictureFiles(result.Document));
            Assert.Equal((600, 450), TestPictures.Size(Odt.Entry(result.Document, "Pictures/Part.Photo.jpg").Bytes));
            var frame = Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();
            Assert.Equal("2in", (string)frame.Attribute(Odt.Svg + "width"));
            Assert.Equal("1.5in", (string)frame.Attribute(Odt.Svg + "height"));
        }

        [Fact]
        public void ATemplateShowsATurnedPhotoUprightAndFitsTheFrameToTheUprightSides()
        {
            var settings = new RenderSettings { PictureProcessor = Scaling };
            var body = P(Odt.Frame("Photo", "Pictures/placeholder.png", "<$Part.Photo fit=\"contain\">", size: "svg:width=\"2in\" svg:height=\"2in\""));
            var model = new Dictionary<string, object> { ["Part"] = new Dictionary<string, object> { ["Photo"] = TestPictures.WithOrientation(TestPictures.Jpeg(1600, 800), 6) } };

            var result = Document(body, model, settings, pictures: Placeholders);

            Assert.Empty(result.Findings);
            Assert.Equal((300, 600), TestPictures.Size(Odt.Entry(result.Document, "Pictures/Part.Photo.jpg").Bytes));
            var frame = Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();
            Assert.Equal("1in", (string)frame.Attribute(Odt.Svg + "width"));
            Assert.Equal("2in", (string)frame.Attribute(Odt.Svg + "height"));
        }

        private static PictureFrame Frame(double width, double height, PictureFit fit) => new PictureFrame("Part.Photo", width, height, fit);
    }
}
