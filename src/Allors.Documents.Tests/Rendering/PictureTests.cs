// <copyright file="PictureTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml.Linq;
    using Allors.Document.Models;
    using Allors.Document.OpenDocument;
    using Allors.Document.Validation;
    using Allos.Document.OpenDocument.Tests;
    using Xunit;
    using static Allos.Document.Rendering.Tests.Render;

    /// <summary>
    /// Pictures under the resolver engine: a frame whose description is a picture tag shows the picture at that path, stored in the package
    /// under a name made of the path, with the media type its bytes say; without a picture the frame leaves the document.
    /// </summary>
    public class PictureTests
    {
        private static readonly XNamespace LoExt = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";

        private static readonly byte[] Placeholder = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        private static readonly byte[] Png = Resource("logo.png");
        private static readonly byte[] Png2 = Resource("1.png");
        private static readonly byte[] Svg = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"navy\"/></svg>");
        private static readonly IDictionary<string, byte[]> Placeholders = new Dictionary<string, byte[]> { ["placeholder.png"] = Placeholder };

        public static IEnumerable<object[]> Formats =>
        [
            [Png, "png", "image/png", true],
            [new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1 }, "jpg", "image/jpeg", true],
            [Encoding.ASCII.GetBytes("GIF89a").Concat(new byte[10]).ToArray(), "gif", "image/gif", true],
            [Encoding.ASCII.GetBytes("RIFF").Concat(new byte[] { 1, 2, 3, 4 }).Concat(Encoding.ASCII.GetBytes("WEBPVP8 ")).ToArray(), "webp", "image/webp", true],
            [new byte[] { 0x42, 0x4D }.Concat(new byte[12]).Concat(BitConverter.GetBytes(40)).Concat(new byte[12]).ToArray(), "bmp", "image/bmp", false],
            [new byte[] { 0x49, 0x49, 0x2A, 0x00, 8, 0, 0, 0 }, "tif", "image/tiff", false],
            [Svg, "svg", "image/svg+xml", false],
            [BitConverter.GetBytes(1).Concat(new byte[36]).Concat(Encoding.ASCII.GetBytes(" EMF")).Concat(new byte[4]).ToArray(), "emf", "image/x-emf", false],
            [new byte[] { 0xD7, 0xCD, 0xC6, 0x9A, 0, 0, 0, 0 }, "wmf", "image/x-wmf", false],
        ];

        [Fact]
        public void ABoundFrameShowsThePictureAtItsPath()
        {
            var body = P(Odt.Frame("Logo", "Pictures/placeholder.png", "<$Issuer.Logo>", "The issuer's logo", "image/png"));
            var model = new Dictionary<string, object> { ["Issuer"] = new Dictionary<string, object> { ["Logo"] = Png } };

            var result = Document(body, model, pictures: Placeholders);

            var frame = Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();
            Assert.Equal("Logo", (string)frame.Attribute(Odt.Draw + "name"));
            var image = frame.Element(Odt.Draw + "image");
            Assert.Equal("Pictures/Issuer.Logo.png", (string)image.Attribute(Odt.XLink + "href"));
            Assert.Equal("image/png", (string)image.Attribute(Odt.Draw + "mime-type"));
            Assert.Equal("The issuer's logo", frame.Element(Odt.Svg + "title").Value);
            Assert.Null(frame.Element(Odt.Svg + "desc"));

            Assert.Equal(Png, Odt.Entry(result.Document, "Pictures/Issuer.Logo.png").Bytes);
            Assert.True(Odt.Entry(result.Document, "Pictures/Issuer.Logo.png").Stored);
            Assert.Equal(["Pictures/Issuer.Logo.png"], Odt.PictureFiles(result.Document));
            Assert.Equal(["Pictures/Issuer.Logo.png"], Odt.ManifestPictures(result.Document));
            Assert.Equal("image/png", Odt.ManifestEntries(result.Document)["Pictures/Issuer.Logo.png"]);
            Assert.Empty(result.Findings);
        }

        [Theory]
        [MemberData(nameof(Formats))]
        public void TheFileNameAndTheMediaTypeFollowTheBytes(byte[] bytes, string extension, string mediaType, bool stored)
        {
            var result = Document(P(Odt.Picture("<$Photo>")), new Dictionary<string, object> { ["Photo"] = bytes }, pictures: Placeholders);

            var name = "Pictures/Photo." + extension;
            Assert.Equal([("Image1", name)], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(bytes, Odt.Entry(result.Document, name).Bytes);
            Assert.Equal(stored, Odt.Entry(result.Document, name).Stored);
            Assert.Equal(mediaType, Odt.ManifestEntries(result.Document)[name]);
            Assert.Equal(mediaType, (string)Odt.Content(result.Document).Descendants(Odt.Draw + "image").Single().Attribute(Odt.Draw + "mime-type"));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void TextThatMerelyMentionsSvgIsNoPicture()
        {
            Assert.Null(PictureFormat.Sniff(Encoding.UTF8.GetBytes("<office:document-content><svg:title>t</svg:title></office:document-content>")));
            Assert.Null(PictureFormat.Sniff(Encoding.UTF8.GetBytes("BMW")));
            Assert.Null(PictureFormat.Sniff(new byte[0]));
            Assert.Null(PictureFormat.Sniff(null));
            Assert.Same(PictureFormat.Svg, PictureFormat.Sniff(Encoding.UTF8.GetBytes("﻿\n<!-- c --><svg:svg xmlns:svg=\"http://www.w3.org/2000/svg\"/>")));
            Assert.Same(PictureFormat.Svg, PictureFormat.Sniff(Encoding.UTF8.GetBytes("<svg/>")));
        }

        [Fact]
        public void APictureInALoopIsStoredOncePerDistinctContentAndTheFramesAreNumbered()
        {
            var body = P("<@for item Items>") + P(Odt.Picture("<$item.Photo>", "Photo")) + P("<@end>");
            var items = new object[] { Photo(Png), Photo(Png2), Photo(Png) };

            var result = Document(body, new Dictionary<string, object> { ["Items"] = items }, pictures: Placeholders);

            Assert.Equal([("Photo", "Pictures/item.Photo.png"), ("Photo 2", "Pictures/item.Photo.2.png"), ("Photo 3", "Pictures/item.Photo.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(["Pictures/item.Photo.png", "Pictures/item.Photo.2.png"], Odt.PictureFiles(result.Document));
            Assert.Equal(["Pictures/item.Photo.png", "Pictures/item.Photo.2.png"], Odt.ManifestPictures(result.Document));
            Assert.Equal(Png2, Odt.Entry(result.Document, "Pictures/item.Photo.2.png").Bytes);
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void TheSameBytesInBodyAndHeaderAreOneFile()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P(Odt.Picture("<$Logo>", "Logo")) + "</style:header></style:master-page></office:master-styles>";

            var result = Document(P(Odt.Picture("<$Logo>", "Logo")), new Dictionary<string, object> { ["Logo"] = Png }, styles: header, pictures: Placeholders);

            Assert.Equal([("Logo", "Pictures/Logo.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal([("Logo 2", "Pictures/Logo.png")], Odt.Frames(Odt.Styles(result.Document)));
            Assert.Equal(["Pictures/Logo.png"], Odt.PictureFiles(result.Document));
            Assert.Equal(["Pictures/Logo.png"], Odt.ManifestPictures(result.Document));
        }

        [Fact]
        public void ANullValueRemovesTheFrameInSilence()
        {
            var result = Document(P("before ", Odt.Picture("<$Logo>"), " after"), new Dictionary<string, object> { ["Logo"] = null }, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal("before  after", Paragraphs(result));
            Assert.Empty(Odt.PictureFiles(result.Document));
            Assert.Empty(Odt.ManifestPictures(result.Document));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void AMemberTheModelDoesNotHaveRemovesTheFrameAndIsReported()
        {
            var result = Document(P(Odt.Picture("<$Lgoo>")), new Dictionary<string, object> { ["Logo"] = Png }, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("<$Lgoo>", finding.Text);
            Assert.Equal("the model has no member Lgoo", finding.Message);
            Assert.Equal(1, finding.Location.Paragraph);
        }

        [Fact]
        public void AValueThatIsNoPictureRemovesTheFrameAndIsReported()
        {
            var result = Document(P(Odt.Picture("<$Logo>")), new Dictionary<string, object> { ["Logo"] = "logo.png" }, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.NotAPicture, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("'Logo' is not a picture: it is a String", finding.Message);
            Assert.Equal("Logo", finding.Path);
            Assert.Equal("<$Logo>", finding.Text);
        }

        [Fact]
        public void BytesInNoKnownFormatRemoveTheFrameAndAreReported()
        {
            var result = Document(P(Odt.Picture("<$Logo>")), new Dictionary<string, object> { ["Logo"] = new byte[] { 1, 2, 3, 4 } }, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.NotAPicture, finding.Kind);
            Assert.Equal("the bytes at 'Logo' are in no picture format the library knows: use PNG, JPEG, GIF, WebP, BMP, TIFF, SVG, EMF or WMF", finding.Message);
        }

        [Theory]
        [InlineData("keep")]
        [InlineData("KEEP")]
        public void EmptyKeepKeepsTheTemplatePictureWhenThereIsNone(string keep)
        {
            var body = P(Odt.Frame("Logo", "Pictures/placeholder.png", "<$Logo empty=\"" + keep + "\">", "The logo"));

            var result = Document(body, new Dictionary<string, object> { ["Logo"] = null }, pictures: Placeholders);

            var frame = Odt.Content(result.Document).Descendants(Odt.Draw + "frame").Single();
            Assert.Equal("Pictures/placeholder.png", (string)frame.Element(Odt.Draw + "image").Attribute(Odt.XLink + "href"));
            Assert.Equal("The logo", frame.Element(Odt.Svg + "title").Value);
            Assert.Null(frame.Element(Odt.Svg + "desc"));
            Assert.Equal(Placeholder, Odt.Entry(result.Document, "Pictures/placeholder.png").Bytes);
            Assert.Equal(["Pictures/placeholder.png"], Odt.ManifestPictures(result.Document));
            Assert.Equal(Odt.Entry(Odt.Build(body, Placeholders), "META-INF/manifest.xml").Bytes, Odt.Entry(result.Document, "META-INF/manifest.xml").Bytes);
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void EmptyKeepStillReportsAMemberTheModelDoesNotHave()
        {
            var result = Document(P(Odt.Picture("<$Lgoo empty=\"keep\">")), new Dictionary<string, object>(), pictures: Placeholders);

            Assert.Equal([("Image1", "Pictures/placeholder.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(FindingKind.UnknownMember, Assert.Single(result.Findings).Kind);
        }

        [Fact]
        public void TemplatePicturesNoFrameShowsAnyMoreLeaveThePackageAndTheManifest()
        {
            var placeholders = new Dictionary<string, byte[]> { ["placeholder.png"] = Placeholder, ["shared.png"] = Png2, ["orphan.png"] = Png };
            var body = P(Odt.Picture("<$Logo>", "Logo")) + P(Odt.Frame("Decoration", "Pictures/shared.png")) + P("<@for item Items>") + P(Odt.Frame("Gone", "Pictures/placeholder.png")) + P("<@end>");

            var result = Document(body, new Dictionary<string, object> { ["Logo"] = Svg, ["Items"] = new object[0] }, pictures: placeholders);

            Assert.Equal(["Pictures/shared.png", "Pictures/Logo.svg"], Odt.PictureFiles(result.Document));
            Assert.Equal(["Pictures/shared.png", "Pictures/Logo.svg"], Odt.ManifestPictures(result.Document));
            Assert.Equal("image/svg+xml", Odt.ManifestEntries(result.Document)["Pictures/Logo.svg"]);
            Assert.False(Odt.Entry(result.Document, "Pictures/Logo.svg").Stored);
            Assert.Equal(Png2, Odt.Entry(result.Document, "Pictures/shared.png").Bytes);
        }

        [Fact]
        public void FrameNamesAreUniqueAcrossTheDocument()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P(Odt.Frame("Logo", "Pictures/placeholder.png")) + "</style:header></style:master-page></office:master-styles>";
            var box = "<draw:frame draw:name=\"Box\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"1cm\"><draw:text-box><text:p>" + Odt.Placeholder("<$item.Name>") + "</text:p></draw:text-box></draw:frame>";
            var body = P("<@for item Items>") + P(Odt.Frame("Logo", "Pictures/placeholder.png") + box) + P("<@end>") + P(Odt.Frame("Logo 2", "Pictures/placeholder.png"));

            var result = Document(body, new Dictionary<string, object> { ["Items"] = new object[] { Item("a"), Item("b") } }, styles: header, pictures: Placeholders);

            Assert.Equal(["Logo", "Box", "Logo 2", "Box 2", "Logo 2 2"], Odt.Frames(Odt.Content(result.Document)).Select(v => v.Name));
            Assert.Equal(["Logo 3"], Odt.Frames(Odt.Styles(result.Document)).Select(v => v.Name));
            Assert.Equal(["a", "b"], Odt.Content(result.Document).Descendants(Odt.Draw + "text-box").Select(v => v.Value));
        }

        [Fact]
        public void ANewFileNeverTakesTheNameOfATemplateFile()
        {
            var result = Document(P(Odt.Picture("<$Logo>", "Logo", "Pictures/Logo.png")), new Dictionary<string, object> { ["Logo"] = Png }, pictures: new Dictionary<string, byte[]> { ["Logo.png"] = Placeholder });

            Assert.Equal([("Logo", "Pictures/Logo.2.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(["Pictures/Logo.2.png"], Odt.PictureFiles(result.Document));
        }

        [Fact]
        public void TheMimeTypeIsRewrittenUnderTheOlderNamespaceToo()
        {
            var frame = "<draw:frame draw:name=\"Logo\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"1cm\" xmlns:loext=\"" + LoExt + "\">" +
                        "<draw:image xlink:href=\"Pictures/placeholder.png\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\" loext:mime-type=\"image/png\"/><svg:desc>&lt;$Logo&gt;</svg:desc></draw:frame>";

            var result = Document(P(frame), new Dictionary<string, object> { ["Logo"] = Svg }, pictures: Placeholders);

            var image = Odt.Content(result.Document).Descendants(Odt.Draw + "image").Single();
            Assert.Equal("Pictures/Logo.svg", (string)image.Attribute(Odt.XLink + "href"));
            Assert.Equal("image/svg+xml", (string)image.Attribute(LoExt + "mime-type"));
        }

        [Fact]
        public void APictureEmbeddedInTheTemplateIsReplacedByTheStoredOne()
        {
            var frame = "<draw:frame draw:name=\"Logo\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"1cm\">" +
                        "<draw:image><office:binary-data>iVBORw0KGgo=</office:binary-data></draw:image><svg:desc>&lt;$Logo&gt;</svg:desc></draw:frame>";

            var result = Document(P(frame), new Dictionary<string, object> { ["Logo"] = Png });

            var image = Odt.Content(result.Document).Descendants(Odt.Draw + "image").Single();
            Assert.Equal("Pictures/Logo.png", (string)image.Attribute(Odt.XLink + "href"));
            Assert.Empty(image.Elements());
            Assert.Equal(Png, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);
            Assert.Equal(["Pictures/Logo.png"], Odt.ManifestPictures(result.Document));
        }

        [Fact]
        public void PicturesInAHeaderBindAndReportInTheirPart()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P(Odt.Picture("<$Logo>", "Logo") + Odt.Picture("<$Nope>", "Nope")) + "</style:header></style:master-page></office:master-styles>";

            var result = Document(P("body"), new Dictionary<string, object> { ["Logo"] = Png }, styles: header, pictures: Placeholders);

            Assert.Equal([("Logo", "Pictures/Logo.png")], Odt.Frames(Odt.Styles(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal("styles.xml", finding.Location.Part);
            Assert.Equal("<$Nope>", finding.Text);
        }

        [Fact]
        public void APictureProblemIsReportedOncePerTag()
        {
            var body = P("<@for item Items>") + P(Odt.Picture("<$item.Photo>", "Photo")) + P("<@end>");
            var items = new object[] { Photo("a"), Photo("b"), new Dictionary<string, object>() };

            var result = Document(body, new Dictionary<string, object> { ["Items"] = items }, pictures: Placeholders);

            Assert.Empty(Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal([FindingKind.NotAPicture, FindingKind.UnknownMember], result.Findings.Select(v => v.Kind));
        }

        [Fact]
        public void TheResolverSaysWhatAPictureIs()
        {
            var settings = new RenderSettings { Resolver = new MediaResolver() };

            var result = Document(P(Odt.Picture("<$Logo>")), new Dictionary<string, object> { ["Logo"] = new Media(Png) }, settings, pictures: Placeholders);

            Assert.Equal([("Image1", "Pictures/Logo.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(Png, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void RendersWithPicturesAreRepeatable()
        {
            var template = Template(P("<@for item Items>") + P(Odt.Picture("<$item.Photo>", "Photo")) + P("<@end>"), pictures: Placeholders);
            var model = new Dictionary<string, object> { ["Items"] = new object[] { Photo(Png), Photo(Png2) } };

            var first = Odt.Entries(template.Render(model, new RenderSettings()).Document);
            var second = Odt.Entries(template.Render(model, new RenderSettings()).Document);

            Assert.Equal(first.Select(v => v.Name), second.Select(v => v.Name));
            Assert.All(first.Zip(second), pair => Assert.Equal(pair.First.Bytes, pair.Second.Bytes));
        }

        private static Dictionary<string, object> Photo(object value) => new Dictionary<string, object> { ["Photo"] = value };

        private static byte[] Resource(string name)
        {
            var assembly = typeof(PictureTests).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(v => v.EndsWith("." + name));
            using var resource = assembly.GetManifestResourceStream(resourceName);
            using var output = new MemoryStream();
            resource.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>A host's own picture type, which its resolver knows how to read.</summary>
        private sealed class Media
        {
            public Media(byte[] bytes) => this.Bytes = bytes;

            public byte[] Bytes { get; }
        }

        private sealed class MediaResolver : DictionaryResolver
        {
            public override byte[] Picture(object value) => (value as Media)?.Bytes;
        }
    }
}
