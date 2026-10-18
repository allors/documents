// <copyright file="PictureProcessorTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Document.OpenDocument;
    using Allors.Document.Tags;
    using Allors.Document.Validation;
    using Allos.Document.OpenDocument.Tests;
    using Xunit;
    using static Allos.Document.Rendering.Tests.Render;

    /// <summary>
    /// The picture processor of the render settings sits between a picture's bytes and the package: it sees the bytes the model or a renderer
    /// gave, their format and the frame, and gives the bytes the package stores.
    /// </summary>
    public class PictureProcessorTests
    {
        private static readonly byte[] Placeholder = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly IDictionary<string, byte[]> Placeholders = new Dictionary<string, byte[]> { ["placeholder.png"] = Placeholder };
        private static readonly byte[] Square = FitTests.Png(100, 100);
        private static readonly byte[] Wide = FitTests.Png(400, 100);
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1 };

        [Fact]
        public void TheProcessorSeesTheBytesTheirFormatAndTheFrameAndTheSameBytesAreStoredAsTheyAre()
        {
            var processor = new ProcessorStub((bytes, format, frame) => bytes);
            var settings = new RenderSettings { PictureProcessor = processor };
            var body = P(Odt.Frame("Logo", "Pictures/placeholder.png", "<$Logo fit=\"contain\">", size: "svg:width=\"2in\" svg:height=\"1in\""));

            var result = Document(body, new Dictionary<string, object> { ["Logo"] = Square }, settings, pictures: Placeholders);

            var (bytes, format, frame) = Assert.Single(processor.Calls);
            Assert.Same(Square, bytes);
            Assert.Same(PictureFormat.Png, format);
            Assert.Equal("Logo", frame.Path);
            Assert.Equal(2.0, frame.Width, 6);
            Assert.Equal(1.0, frame.Height, 6);
            Assert.Equal(PictureFit.Contain, frame.Fit);
            Assert.Equal(Square, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);
            Assert.Equal(("1in", "1in"), Size(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void NullFromTheProcessorStoresTheBytesAsTheyAre()
        {
            var settings = new RenderSettings { PictureProcessor = new ProcessorStub((bytes, format, frame) => null) };

            var result = Document(P(Odt.Picture("<$Logo>")), new Dictionary<string, object> { ["Logo"] = Square }, settings, pictures: Placeholders);

            Assert.Equal(Square, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void TheBytesTheProcessorGivesAreStoredUnderTheirOwnFormat()
        {
            var settings = new RenderSettings { PictureProcessor = new ProcessorStub((bytes, format, frame) => Jpeg) };

            var result = Document(P(Odt.Picture("<$Logo>")), new Dictionary<string, object> { ["Logo"] = Square }, settings, pictures: Placeholders);

            Assert.Equal([("Image1", "Pictures/Logo.jpg")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(Jpeg, Odt.Entry(result.Document, "Pictures/Logo.jpg").Bytes);
            Assert.Equal("image/jpeg", Odt.ManifestEntries(result.Document)["Pictures/Logo.jpg"]);
            Assert.Equal("image/jpeg", (string)Odt.Content(result.Document).Descendants(Odt.Draw + "image").Single().Attribute(Odt.Draw + "mime-type"));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void TheFitUsesTheBytesTheProcessorGives()
        {
            var settings = new RenderSettings { PictureProcessor = new ProcessorStub((bytes, format, frame) => Wide) };
            var body = P(Odt.Frame("Logo", "Pictures/placeholder.png", "<$Logo fit=\"contain\">", size: "svg:width=\"2cm\" svg:height=\"2cm\""));

            var result = Document(body, new Dictionary<string, object> { ["Logo"] = Square }, settings, pictures: Placeholders);

            Assert.Equal(("2cm", "0.5cm"), Size(result));
            Assert.Equal(Wide, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);
        }

        [Fact]
        public void AProcessorThatThrowsIsAWarningAndThePictureIsStoredAsItWas()
        {
            var settings = new RenderSettings { PictureProcessor = new ProcessorStub((bytes, format, frame) => throw new InvalidOperationException("boom")) };

            var result = Document(P(Odt.Picture("<$Logo>")), new Dictionary<string, object> { ["Logo"] = Square }, settings, pictures: Placeholders);

            Assert.Equal(Square, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.RendererFailed, finding.Kind);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Equal("the picture processor failed on 'Logo' and the picture is stored as it was: boom", finding.Message);
            Assert.Equal("Logo", finding.Path);
            Assert.Equal("<$Logo>", finding.Text);
        }

        [Fact]
        public void BytesInNoKnownFormatFromTheProcessorAreAWarningAndThePictureIsStoredAsItWas()
        {
            var settings = new RenderSettings { PictureProcessor = new ProcessorStub((bytes, format, frame) => new byte[] { 1, 2, 3 }) };

            var result = Document(P(Odt.Picture("<$Logo>")), new Dictionary<string, object> { ["Logo"] = Square }, settings, pictures: Placeholders);

            Assert.Equal(Square, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.RendererFailed, finding.Kind);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Equal("the picture processor gave bytes in no picture format the library knows for 'Logo' and the picture is stored as it was", finding.Message);
        }

        [Fact]
        public void TheProcessorRunsAfterAPictureRenderer()
        {
            var bars = FitTests.Png(320, 80);
            var processor = new ProcessorStub((bytes, format, frame) => Wide);
            var settings = new RenderSettings { Renderers = { new PictureRendererStub("code128", (value, frame) => bars) }, PictureProcessor = processor };
            var body = P(Odt.Frame("Barcode", "Pictures/placeholder.png", "<$Number format=\"code128\">", size: "svg:width=\"2.44in\" svg:height=\"0.32in\""));

            var result = Document(body, new Dictionary<string, object> { ["Number"] = "1" }, settings, pictures: Placeholders);

            var (bytes, format, frame) = Assert.Single(processor.Calls);
            Assert.Same(bars, bytes);
            Assert.Same(PictureFormat.Png, format);
            Assert.Equal(2.44, frame.Width, 6);
            Assert.Equal(Wide, Odt.Entry(result.Document, "Pictures/Number.png").Bytes);
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void EqualBytesFromTheProcessorAreStoredOnce()
        {
            var settings = new RenderSettings { PictureProcessor = new ProcessorStub((bytes, format, frame) => Wide) };
            var body = P(Odt.Picture("<$A>", "A")) + P(Odt.Picture("<$B>", "B"));

            var result = Document(body, new Dictionary<string, object> { ["A"] = Square, ["B"] = FitTests.Png(10, 10) }, settings, pictures: Placeholders);

            Assert.Equal([("A", "Pictures/A.png"), ("B", "Pictures/A.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(["Pictures/A.png"], Odt.PictureFiles(result.Document));
        }

        [Fact]
        public void TheProcessorIsNotCalledWithoutAPicture()
        {
            var processor = new ProcessorStub((bytes, format, frame) => bytes);
            var settings = new RenderSettings { PictureProcessor = processor };
            var body = P(Odt.Picture("<$Nothing>", "A")) + P(Odt.Picture("<$Text>", "B")) + P(Odt.Picture("<$Nope>", "C")) + P(Odt.Picture("<$Garbage>", "D"));
            var model = new Dictionary<string, object> { ["Nothing"] = null, ["Text"] = "logo.png", ["Garbage"] = new byte[] { 1, 2, 3 } };

            var result = Document(body, model, settings, pictures: Placeholders);

            Assert.Empty(processor.Calls);
            Assert.Equal([FindingKind.NotAPicture, FindingKind.UnknownMember, FindingKind.NotAPicture], result.Findings.Select(v => v.Kind));
        }

        [Fact]
        public void AProcessorProblemIsReportedOncePerTag()
        {
            var settings = new RenderSettings { PictureProcessor = new ProcessorStub((bytes, format, frame) => throw new InvalidOperationException("boom")) };
            var body = P("<@for item Items>") + P(Odt.Picture("<$item.Photo>", "Photo")) + P("<@end>");
            var items = new object[] { new Dictionary<string, object> { ["Photo"] = Square }, new Dictionary<string, object> { ["Photo"] = Wide } };

            var result = Document(body, new Dictionary<string, object> { ["Items"] = items }, settings, pictures: Placeholders);

            Assert.Equal(2, Odt.Frames(Odt.Content(result.Document)).Count);
            Assert.Single(result.Findings);
        }

        private static (string Width, string Height) Size(RenderResult result)
        {
            var frame = Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();
            return ((string)frame.Attribute(Odt.Svg + "width"), (string)frame.Attribute(Odt.Svg + "height"));
        }
    }
}
