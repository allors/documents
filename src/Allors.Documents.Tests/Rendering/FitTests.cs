// <copyright file="FitTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using Allors.Documents;
    using Xunit;
    using static Allors.Documents.Tests.Render;

    /// <summary>
    /// The fit attribute of a picture tag: fill, the default, stretches the picture to the frame as drawn; contain shrinks the side that would
    /// overflow; width and height keep that side and let the other follow the picture's proportions. Only the side that changes is rewritten.
    /// </summary>
    public class FitTests
    {
        private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

        private static readonly byte[] Placeholder = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly IDictionary<string, byte[]> Placeholders = new Dictionary<string, byte[]> { ["placeholder.png"] = Placeholder };

        [Theory]
        [InlineData("contain", 100, 100, "1cm", "1cm")]
        [InlineData("contain", 400, 100, "2cm", "0.5cm")]
        [InlineData("contain", 200, 100, "2cm", "1cm")]
        [InlineData("CONTAIN", 100, 100, "1cm", "1cm")]
        [InlineData("width", 100, 100, "2cm", "2cm")]
        [InlineData("width", 400, 100, "2cm", "0.5cm")]
        [InlineData("height", 400, 100, "4cm", "1cm")]
        [InlineData("height", 100, 400, "0.25cm", "1cm")]
        [InlineData("fill", 100, 100, "2cm", "1cm")]
        public void TheFrameTakesTheSizeTheFitGivesItForThePicturesProportions(string fit, int width, int height, string frameWidth, string frameHeight)
        {
            var result = Document(P(Frame("<$Logo fit=\"" + fit + "\">")), Model(Png(width, height)), pictures: Placeholders);

            Assert.Equal((frameWidth, frameHeight), Size(Frame(result)));
            Assert.Equal("Pictures/Logo.png", (string)Frame(result).Element(Odt.Draw + "image").Attribute(Odt.XLink + "href"));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void WithoutAFitThePictureFillsTheFrameAsDrawn()
        {
            var result = Document(P(Frame("<$Logo>")), Model(Png(100, 100)), pictures: Placeholders);

            Assert.Equal(("2cm", "1cm"), Size(Frame(result)));
            Assert.Contains("svg:width=\"2cm\" svg:height=\"1cm\"", Encoding.UTF8.GetString(Odt.Entry(result.Document, "content.xml").Bytes));
        }

        [Theory]
        [InlineData("contain", "svg:width=\"1in\" svg:height=\"2cm\"", "0.7874in", "2cm")]
        [InlineData("width", "svg:width=\"2in\" svg:height=\"1cm\"", "2in", "5.08cm")]
        [InlineData("height", "svg:width=\"72pt\" svg:height=\"3mm\"", "8.5039pt", "3mm")]
        [InlineData("contain", "svg:width=\"2.44in\" svg:height=\"2.44in\"", "2.44in", "2.44in")]
        public void AChangedSideIsWrittenInItsOwnUnitWithUpToFourDecimals(string fit, string size, string frameWidth, string frameHeight)
        {
            var result = Document(P(Frame("<$Logo fit=\"" + fit + "\">", size)), Model(Png(100, 100)), pictures: Placeholders);

            Assert.Equal((frameWidth, frameHeight), Size(Frame(result)));
        }

        [Fact]
        public void ARelativeSizeOnTheChangedSideIsDroppedAndTheOtherSideKeepsIts()
        {
            var size = "svg:width=\"2cm\" style:rel-width=\"scale\" svg:height=\"1cm\" style:rel-height=\"scale\"";

            var result = Document(P(Frame("<$Logo fit=\"contain\">", size)), Model(Png(400, 100)), pictures: Placeholders);

            var frame = Frame(result);
            Assert.Equal(("2cm", "0.5cm"), Size(frame));
            Assert.Equal("scale", (string)frame.Attribute(Style + "rel-width"));
            Assert.Null(frame.Attribute(Style + "rel-height"));
        }

        [Fact]
        public void EachPictureInALoopTakesItsOwnSize()
        {
            var body = P("<@for item Items>") + P(Frame("<$item.Photo fit=\"height\">", name: "Photo")) + P("<@end>");
            var items = new object[] { new Dictionary<string, object> { ["Photo"] = Png(100, 100) }, new Dictionary<string, object> { ["Photo"] = Png(400, 100) } };

            var result = Document(body, new Dictionary<string, object> { ["Items"] = items }, pictures: Placeholders);

            Assert.Equal([("1cm", "1cm"), ("4cm", "1cm")], Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Select(Size));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void APictureWhoseBytesDoNotTellItsSizeFillsTheFrameAndIsReportedOncePerTag()
        {
            var body = P("<@for item Items>") + P(Frame("<$item.Photo fit=\"contain\">", name: "Photo")) + P("<@end>");
            var items = new object[] { new Dictionary<string, object> { ["Photo"] = Placeholder }, new Dictionary<string, object> { ["Photo"] = Placeholder } };

            var result = Document(body, new Dictionary<string, object> { ["Items"] = items }, pictures: Placeholders);

            Assert.Equal([("2cm", "1cm"), ("2cm", "1cm")], Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Select(Size));
            Assert.Equal([("Photo", "Pictures/item.Photo.png"), ("Photo 2", "Pictures/item.Photo.png")], Odt.Frames(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.CannotFit, finding.Kind);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Equal("the picture at 'item.Photo' fills its frame instead of fitting it: its PNG bytes do not tell its size", finding.Message);
            Assert.Equal("<$item.Photo fit=\"contain\">", finding.Text);
            Assert.Equal("item.Photo", finding.Path);
            Assert.Equal(2, finding.Location.Paragraph);
        }

        [Theory]
        [InlineData("svg:width=\"2cm\"")]
        [InlineData("svg:width=\"2cm\" svg:height=\"50%\"")]
        [InlineData("")]
        public void AFrameWithoutAWidthAndAHeightFillsAndIsReported(string size)
        {
            var result = Document(P(Frame("<$Logo fit=\"contain\">", size)), Model(Png(100, 100)), pictures: Placeholders);

            Assert.Equal("Pictures/Logo.png", (string)Frame(result).Element(Odt.Draw + "image").Attribute(Odt.XLink + "href"));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.CannotFit, finding.Kind);
            Assert.Equal("the picture at 'Logo' fills its frame instead of fitting it: the frame has no width and height", finding.Message);
        }

        [Fact]
        public void EmptyKeepLeavesTheTemplatePictureAsDrawn()
        {
            var result = Document(P(Frame("<$Logo fit=\"contain\" empty=\"keep\">")), Model(null), pictures: Placeholders);

            Assert.Equal(("2cm", "1cm"), Size(Frame(result)));
            Assert.Equal("Pictures/placeholder.png", (string)Frame(result).Element(Odt.Draw + "image").Attribute(Odt.XLink + "href"));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void AValueTheFitAttributeDoesNotTakeFillsAtRenderAndIsForValidateToReport()
        {
            var result = Document(P(Frame("<$Logo fit=\"cover\">")), Model(Png(100, 100)), pictures: Placeholders);

            Assert.Equal(("2cm", "1cm"), Size(Frame(result)));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ThePictureInAHeaderFitsToo()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P(Frame("<$Logo fit=\"contain\">")) + "</style:header></style:master-page></office:master-styles>";

            var result = Document(P("body"), Model(Png(100, 100)), styles: header, pictures: Placeholders);

            Assert.Equal(("1cm", "1cm"), Size(Odt.Styles(result.Document).Descendants(Odt.Draw + "frame").Single()));
        }

        /// <summary>A PNG header: the signature and an IHDR chunk stating the size; enough for the library, which reads no pixels.</summary>
        internal static byte[] Png(int width, int height)
        {
            var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
            bytes.AddRange(new[] { (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width });
            bytes.AddRange(new[] { (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height });
            bytes.AddRange(new byte[] { 8, 2, 0, 0, 0, 0, 0, 0, 0 });
            return bytes.ToArray();
        }

        private static Dictionary<string, object> Model(byte[] logo) => new Dictionary<string, object> { ["Logo"] = logo };

        /// <summary>A bound picture frame with the given size attributes, 2cm by 1cm by default.</summary>
        private static string Frame(string tag, string size = "svg:width=\"2cm\" svg:height=\"1cm\"", string name = "Logo") =>
            "<draw:frame draw:name=\"" + name + "\" text:anchor-type=\"paragraph\" " + size + ">" +
            "<draw:image xlink:href=\"Pictures/placeholder.png\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\" draw:mime-type=\"image/png\"/>" +
            "<svg:desc>" + new XText(tag) + "</svg:desc></draw:frame>";

        private static XElement Frame(RenderResult result) => Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();

        private static (string Width, string Height) Size(XElement frame) => ((string)frame.Attribute(Odt.Svg + "width"), (string)frame.Attribute(Odt.Svg + "height"));
    }
}
