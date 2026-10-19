// <copyright file="PictureRendererTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;
    using Allors.Documents;
    using Xunit;
    using static Allors.Documents.Tests.Render;

    /// <summary>
    /// The format attribute of a picture tag names a picture renderer: the frame shows what the renderer draws for the value, sized for the
    /// frame as the author drew it, instead of the picture the resolver would give.
    /// </summary>
    public class PictureRendererTests
    {
        private static readonly byte[] Placeholder = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly IDictionary<string, byte[]> Placeholders = new Dictionary<string, byte[]> { ["placeholder.png"] = Placeholder };
        private static readonly byte[] Bars = FitTests.Png(320, 80);

        [Fact]
        public void FormatOnAFrameSelectsThePictureRendererWhichGetsTheValueAndTheFrame()
        {
            var barcode = new PictureRendererStub("code128", (value, text, frame) => Bars);
            var settings = new RenderSettings { Renderers = [barcode] };
            var body = P(Odt.Frame("Barcode", "Pictures/placeholder.png", "<$Invoice.Number format=\"code128\" empty=\"remove\">", size: "svg:width=\"2.44in\" svg:height=\"0.32in\""));
            var model = new Dictionary<string, object> { ["Invoice"] = new Dictionary<string, object> { ["Number"] = "INV-1" } };

            var result = Document(body, model, settings, pictures: Placeholders);

            var (value, text, frame) = Assert.Single(barcode.Calls);
            Assert.Equal("INV-1", value);
            Assert.Equal("INV-1", text);
            Assert.Equal("Invoice.Number", frame.Path);
            Assert.Equal(2.44, frame.Width, 6);
            Assert.Equal(0.32, frame.Height, 6);
            Assert.Equal(PictureFit.Fill, frame.Fit);
            Assert.True(frame.HasSize);
            Assert.Equal("Invoice.Number 2.44x0.32in fill", frame.ToString());
            Assert.Equal([("Barcode", "Pictures/Invoice.Number.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(Bars, Odt.Entry(result.Document, "Pictures/Invoice.Number.png").Bytes);
            Assert.Equal("image/png", Odt.ManifestEntries(result.Document)["Pictures/Invoice.Number.png"]);
            Assert.Equal(("2.44in", "0.32in"), Size(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void TheRendererGetsTheValuesTextUnderTheLibrarysDefaultsWhateverTheCulture()
        {
            var barcode = new PictureRendererStub("code128", (value, text, frame) => Bars);
            var settings = new RenderSettings { Culture = System.Globalization.CultureInfo.GetCultureInfo("nl-BE"), Number = "N2", Renderers = [barcode] };
            var body = P(Odt.Picture("<$Amount format=\"code128\">")) + P(Odt.Picture("<$Paid format=\"code128\">"));
            var model = new Dictionary<string, object> { ["Amount"] = 1234.5m, ["Paid"] = true };

            var result = Document(body, model, settings);

            Assert.Equal(["1234.5", "true"], barcode.Calls.Select(v => v.Text));
            Assert.Equal([1234.5m, true], barcode.Calls.Select(v => v.Value));
            Assert.Empty(result.Findings);
        }

        [Theory]
        [InlineData("svg:width=\"5.08cm\" svg:height=\"2.54cm\"", 2.0, 1.0)]
        [InlineData("svg:width=\"72pt\" svg:height=\"12pc\"", 1.0, 2.0)]
        [InlineData("svg:width=\"96px\" svg:height=\"25.4mm\"", 1.0, 1.0)]
        public void TheFrameSizeIsInInchesWhateverTheUnit(string size, double width, double height)
        {
            var frame = Frame("<$Logo format=\"code128\">", size);

            Assert.Equal(width, frame.Width, 6);
            Assert.Equal(height, frame.Height, 6);
            Assert.True(frame.HasSize);
        }

        [Theory]
        [InlineData("svg:width=\"2cm\"", 0.7874, 0)]
        [InlineData("svg:width=\"2cm\" svg:height=\"50%\"", 0.7874, 0)]
        [InlineData("svg:width=\"0cm\" svg:height=\"1cm\"", 0, 0.3937)]
        [InlineData("", 0, 0)]
        public void ASideWithoutAnAbsoluteLengthIsZero(string size, double width, double height)
        {
            var frame = Frame("<$Logo format=\"code128\">", size);

            Assert.Equal(width, frame.Width, 4);
            Assert.Equal(height, frame.Height, 4);
            Assert.False(frame.HasSize);
            Assert.Equal("Logo without a size fill", frame.ToString());
        }

        [Fact]
        public void TheFitOfTheTagIsPassedOnAndAppliesToWhatTheRendererDraws()
        {
            var barcode = new PictureRendererStub("code128", (value, text, frame) => Bars);
            var settings = new RenderSettings { Renderers = [barcode] };
            var body = P(Odt.Frame("Logo", "Pictures/placeholder.png", "<$Number format=\"code128\" fit=\"contain\">", size: "svg:width=\"2cm\" svg:height=\"2cm\""));

            var result = Document(body, new Dictionary<string, object> { ["Number"] = "1" }, settings, pictures: Placeholders);

            Assert.Equal(PictureFit.Contain, Assert.Single(barcode.Calls).Frame.Fit);
            Assert.Equal(("2cm", "0.5cm"), Size(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void NullFromTheRendererMeansNoPictureInSilence()
        {
            var settings = new RenderSettings { Renderers = [new PictureRendererStub("code128", (value, text, frame) => null)] };
            var model = new Dictionary<string, object> { ["Number"] = string.Empty };

            var removed = Document(P(Odt.Picture("<$Number format=\"code128\">")), model, settings, pictures: Placeholders);
            var kept = Document(P(Odt.Picture("<$Number format=\"code128\" empty=\"keep\">")), model, settings, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(removed.Document)));
            Assert.Empty(Odt.PictureFiles(removed.Document));
            Assert.Empty(removed.Findings);
            Assert.Equal([("Image1", "Pictures/placeholder.png")], Odt.Frames(Odt.Content(kept.Document)));
            Assert.Empty(kept.Findings);
        }

        [Fact]
        public void ARendererThatThrowsIsReportedAndTheFrameHasNoPicture()
        {
            var settings = new RenderSettings { Renderers = [new PictureRendererStub("code128", (value, text, frame) => throw new InvalidOperationException("boom"))] };
            var model = new Dictionary<string, object> { ["Number"] = "1" };

            var removed = Document(P(Odt.Picture("<$Number format=\"code128\">")), model, settings, pictures: Placeholders);
            var kept = Document(P(Odt.Picture("<$Number format=\"code128\" empty=\"keep\">")), model, settings, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(removed.Document)));
            var finding = Assert.Single(removed.Findings);
            Assert.Equal(FindingKind.RendererFailed, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("the renderer 'code128' failed on 'Number' and the frame has no picture: boom", finding.Message);
            Assert.Equal("Number", finding.Path);
            Assert.Equal("<$Number format=\"code128\">", finding.Text);
            Assert.Equal([("Image1", "Pictures/placeholder.png")], Odt.Frames(Odt.Content(kept.Document)));
            Assert.Equal(FindingKind.RendererFailed, Assert.Single(kept.Findings).Kind);
        }

        [Fact]
        public void BytesInNoKnownFormatFromTheRendererAreReported()
        {
            var settings = new RenderSettings { Renderers = [new PictureRendererStub("code128", (value, text, frame) => new byte[] { 1, 2, 3 })] };

            var result = Document(P(Odt.Picture("<$Number format=\"code128\">")), new Dictionary<string, object> { ["Number"] = "1" }, settings, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.RendererFailed, finding.Kind);
            Assert.Equal("the renderer 'code128' gave bytes in no picture format the library knows for 'Number' and the frame has no picture", finding.Message);
        }

        [Fact]
        public void ATextRendererOnAFrameIsReportedAndTheFrameShowsTheResolversPicture()
        {
            var model = new Dictionary<string, object> { ["Logo"] = Bars };

            var alone = Document(P(Odt.Picture("<$Logo format=\"upper\">")), model, pictures: Placeholders);
            var withBarcode = Document(P(Odt.Picture("<$Logo format=\"upper\">")), model, new RenderSettings { Renderers = [new PictureRendererStub("code128", (value, text, frame) => null)] }, pictures: Placeholders);

            Assert.Equal([("Image1", "Pictures/Logo.png")], Odt.Frames(Odt.Content(alone.Document)));
            Assert.Equal(Bars, Odt.Entry(alone.Document, "Pictures/Logo.png").Bytes);
            var finding = Assert.Single(alone.Findings);
            Assert.Equal(FindingKind.BadValue, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("format=\"upper\" renders text, not a picture: none is registered", finding.Message);
            Assert.Equal("Logo", finding.Path);
            Assert.Equal("format=\"upper\" renders text, not a picture: use code128", Assert.Single(withBarcode.Findings).Message);
        }

        [Fact]
        public void ANameNoRendererHasIsReportedAndTheFrameShowsTheResolversPicture()
        {
            var settings = new RenderSettings { Renderers = [new PictureRendererStub("code128", (value, text, frame) => null), new PictureRendererStub("qr", (value, text, frame) => null)] };

            var result = Document(P(Odt.Picture("<$Logo format=\"nope\">")), new Dictionary<string, object> { ["Logo"] = Bars }, settings, pictures: Placeholders);

            Assert.Equal([("Image1", "Pictures/Logo.png")], Odt.Frames(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.BadValue, finding.Kind);
            Assert.Equal("format=\"nope\" names no picture renderer: use code128 or qr", finding.Message);
        }

        [Fact]
        public void TheRendererRunsPerItemAndEqualPicturesAreStoredOnce()
        {
            var barcode = new PictureRendererStub("code128", (value, text, frame) => FitTests.Png((string)value == "a" ? 1 : 2, 1));
            var settings = new RenderSettings { Renderers = [barcode] };
            var body = P("<@for item Items>") + P(Odt.Picture("<$item format=\"code128\">", "Code")) + P("<@end>");

            var result = Document(body, new Dictionary<string, object> { ["Items"] = new object[] { "a", "b", "a" } }, settings, pictures: Placeholders);

            Assert.Equal(["a", "b", "a"], barcode.Calls.Select(v => v.Value));
            Assert.Equal([("Code", "Pictures/item.png"), ("Code 2", "Pictures/item.2.png"), ("Code 3", "Pictures/item.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(["Pictures/item.png", "Pictures/item.2.png"], Odt.PictureFiles(result.Document));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ARendererThatIsTextAndPictureServesBothTags()
        {
            var qr = new BothRendererStub("qr", (value, text, formatting) => "QR:" + text, (value, text, frame) => Bars);
            var settings = new RenderSettings { Renderers = [qr] };
            var body = P("<$Code format=\"qr\">") + P(Odt.Picture("<$Code format=\"qr\">"));

            var result = Document(body, new Dictionary<string, object> { ["Code"] = "x" }, settings, pictures: Placeholders);

            Assert.Equal("QR:x\n", Paragraphs(result));
            Assert.Equal([("Image1", "Pictures/Code.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void AnUnknownMemberIsReportedAndTheRendererDoesNotRun()
        {
            var barcode = new PictureRendererStub("code128", (value, text, frame) => Bars);
            var settings = new RenderSettings { Renderers = [barcode] };

            var result = Document(P(Odt.Picture("<$Nope format=\"code128\">")), new Dictionary<string, object>(), settings, pictures: Placeholders);

            Assert.Empty(barcode.Calls);
            Assert.Equal(FindingKind.UnknownMember, Assert.Single(result.Findings).Kind);
        }

        /// <summary>The frame a renderer sees for a tag in a frame of this size.</summary>
        private static PictureFrame Frame(string tag, string size)
        {
            var barcode = new PictureRendererStub("code128", (value, text, frame) => Bars);
            var settings = new RenderSettings { Renderers = [barcode] };

            Document(P(Odt.Frame("Logo", "Pictures/placeholder.png", tag, size: size)), new Dictionary<string, object> { ["Logo"] = "x" }, settings, pictures: Placeholders);

            return Assert.Single(barcode.Calls).Frame;
        }

        private static (string Width, string Height) Size(RenderResult result)
        {
            var frame = Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();
            return ((string)frame.Attribute(Odt.Svg + "width"), (string)frame.Attribute(Odt.Svg + "height"));
        }
    }
}
