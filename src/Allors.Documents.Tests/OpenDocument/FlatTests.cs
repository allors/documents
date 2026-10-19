// <copyright file="FlatTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Xml.Linq;
    using Allors.Documents;
    using Allors.Documents.Reflection;
    using Xunit;

    /// <summary>
    /// Flat documents (.fodt), one XML file with the pictures embedded: a template in that form renders like a package, and a render gives
    /// that form when asked.
    /// </summary>
    public class FlatTests
    {
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        private static readonly byte[] OtherPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 };

        private static readonly RenderSettings Flat = new RenderSettings { Flat = true };

        private static readonly Model Model = new Model
        {
            Person = new ModelPerson { FirstName = "Jane" },
            People = [new ModelPerson { FirstName = "John" }, new ModelPerson { FirstName = "Jenny" }],
            Images = ["number1", "number2", "number3"],
        };

        [Fact]
        public void AFlatTemplateRendersToAPackage()
        {
            var template = Fodt.Build(Fodt.P("Hello ", "<$Name>"), Fodt.H("Page of ", "<$Name>"));

            var result = new OpenDocumentTemplate(template).Render(new Dictionary<string, object> { ["Name"] = "Jane" });

            var entries = Odt.Entries(result.Document);
            Assert.Equal(["mimetype", "content.xml", "styles.xml", "META-INF/manifest.xml", "meta.xml", "settings.xml"], entries.Select(v => v.Name));
            Assert.True(entries[0].Stored);
            Assert.Equal(Odt.MediaType, Encoding.ASCII.GetString(entries[0].Bytes));
            Assert.Equal("Hello Jane", Odt.Paragraphs(Odt.Content(result.Document)));
            Assert.Equal("Page of Jane", Odt.Paragraphs(Odt.Styles(result.Document)));
            Assert.Equal("1.3", (string)Odt.Content(result.Document).Root.Attribute(Odt.Office + "version"));
            Assert.Equal("1.3", (string)Odt.Styles(result.Document).Root.Attribute(Odt.Office + "version"));

            var manifest = Odt.ManifestEntries(result.Document);
            Assert.Equal(Odt.MediaType, manifest["/"]);
            Assert.All(new[] { "content.xml", "styles.xml", "meta.xml", "settings.xml" }, v => Assert.Equal("text/xml", manifest[v]));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void AutomaticStylesGoToThePartThatUsesThem()
        {
            var automaticStyles =
                "  <style:style style:name=\"P1\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"/>\n" +
                "  <style:style style:name=\"MP1\" style:family=\"paragraph\" style:parent-style-name=\"Header\"/>\n" +
                "  <style:style style:name=\"T1\" style:family=\"text\"/>\n" +
                "  <style:style style:name=\"X1\" style:family=\"text\"/>\n" +
                "  <style:style style:name=\"P2\" style:family=\"paragraph\" style:list-style-name=\"L1\"/>\n" +
                "  <text:list-style style:name=\"L1\"/>\n" +
                "  <style:page-layout style:name=\"pm1\"/>\n";
            var body = Fodt.P("a <text:span text:style-name=\"T1\">b</text:span>") + "\n" + Fodt.Paragraph("P2", "c");
            var header = Fodt.H("<text:span text:style-name=\"T1\">h</text:span>");
            var template = Fodt.Build(body, header, automaticStyles);

            var result = new OpenDocumentTemplate(template).Render(new Dictionary<string, object>());

            // The body uses P1, T1 and P2, which uses L1; nothing uses X1, which goes with the body; the header uses MP1 and T1, the master page pm1
            var content = Odt.Content(result.Document);
            var styles = Odt.Styles(result.Document);
            Assert.Equal(["P1", "T1", "X1", "P2", "L1"], Fodt.AutomaticStyles(content));
            Assert.Equal(["MP1", "T1", "pm1"], Fodt.AutomaticStyles(styles));
            Assert.Equal(["scripts", "font-face-decls", "automatic-styles", "body"], content.Root.Elements().Select(v => v.Name.LocalName));
            Assert.Equal(["font-face-decls", "styles", "automatic-styles", "master-styles"], styles.Root.Elements().Select(v => v.Name.LocalName));
            Assert.Equal("a b\nc", Odt.Paragraphs(content));
            Assert.Equal("h", Odt.Paragraphs(styles));
        }

        [Fact]
        public void EmbeddedPicturesBecomeFilesOfThePackage()
        {
            var body = Fodt.P(Fodt.Frame("Logo", Png)) + "\n" + Fodt.P(Fodt.Frame("Logo again", Png));
            var header = Fodt.H(Fodt.Frame("Mark", OtherPng));
            var template = Fodt.Build(body, header);

            var result = new OpenDocumentTemplate(template).Render(new Dictionary<string, object>());

            // A picture embedded twice is one file, named by its bytes; the frames link to the files the way a package has it
            var frames = Odt.Frames(Odt.Content(result.Document));
            Assert.Equal(["Logo", "Logo again"], frames.Select(v => v.Name));
            Assert.Equal(frames[0].Href, frames[1].Href);
            Assert.StartsWith("Pictures/", frames[0].Href);
            Assert.EndsWith(".png", frames[0].Href);
            var mark = Assert.Single(Odt.Frames(Odt.Styles(result.Document)));
            Assert.NotEqual(frames[0].Href, mark.Href);
            Assert.Equal(Png, Odt.Entry(result.Document, frames[0].Href).Bytes);
            Assert.Equal(OtherPng, Odt.Entry(result.Document, mark.Href).Bytes);
            Assert.Equal(2, Odt.PictureFiles(result.Document).Count);
            Assert.Equal(["image/png", "image/png"], Odt.ManifestPictures(result.Document).Select(v => Odt.ManifestEntries(result.Document)[v]));

            var image = Odt.Content(result.Document).Descendants(Odt.Draw + "image").First();
            Assert.Equal("simple", (string)image.Attribute(Odt.XLink + "type"));
            Assert.Equal("embed", (string)image.Attribute(Odt.XLink + "show"));
            Assert.Equal("onLoad", (string)image.Attribute(Odt.XLink + "actuate"));
            Assert.Equal("image/png", (string)image.Attribute(Odt.Draw + "mime-type"));
            Assert.Empty(image.Nodes());
            Assert.Empty(Odt.Content(result.Document).Descendants(Odt.Office + "binary-data"));
        }

        [Fact]
        public void ABoundPictureOfAFlatTemplateShowsThePictureOfTheModel()
        {
            var template = Fodt.Build(Fodt.P(Fodt.Frame("Image1", Png, "<$Logo>")));

            var result = new OpenDocumentTemplate(template).Render(new Dictionary<string, object> { ["Logo"] = OtherPng });

            var frame = Assert.Single(Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal(("Image1", "Pictures/Logo.png"), frame);
            Assert.Equal(OtherPng, Odt.Entry(result.Document, "Pictures/Logo.png").Bytes);

            // The placeholder the template embedded is shown by no frame any more and leaves the package with its manifest entry
            Assert.Equal(["Pictures/Logo.png"], Odt.PictureFiles(result.Document));
            Assert.Equal(["Pictures/Logo.png"], Odt.ManifestPictures(result.Document));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void TheDocumentSettingsOfAFlatTemplateApply()
        {
            var template = Fodt.Build(Fodt.P("<$Total>"), settings: new Dictionary<string, string> { ["number"] = "N2" });

            var result = new OpenDocumentTemplate(template).Render(new Dictionary<string, object> { ["Total"] = 1234.5m });

            Assert.Equal("1,234.50", Odt.Paragraphs(Odt.Content(result.Document)));
            Assert.Equal("N2", new OpenDocumentTemplate(template).Inspection.Settings["number"]);
        }

        [Fact]
        public void ARenderGivesAFlatDocumentOnRequest()
        {
            var pictures = new Dictionary<string, byte[]> { ["placeholder.png"] = Png };
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header><text:p>Page " + Odt.Placeholder("<$Name>") + "</text:p></style:header></style:master-page></office:master-styles>";
            var body = "<text:p>Hello " + Odt.Placeholder("<$Name>") + "</text:p><text:p>" + Odt.Picture("<$Logo>") + Odt.Frame("Plain", "Pictures/placeholder.png") + "</text:p>";
            var package = Odt.Build(body, pictures, header, settings: new Dictionary<string, string> { ["number"] = "N2" });
            var model = new Dictionary<string, object> { ["Name"] = "Jane", ["Logo"] = OtherPng };

            var result = new OpenDocumentTemplate(package, Flat).Render(model);

            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<office:document ", Encoding.UTF8.GetString(result.Document));
            var flat = Fodt.Document(result.Document);
            Assert.Equal(Odt.Office + "document", flat.Root.Name);
            Assert.Equal(Odt.MediaType, (string)flat.Root.Attribute(Odt.Office + "mimetype"));
            Assert.Equal("1.3", (string)flat.Root.Attribute(Odt.Office + "version"));
            Assert.Equal(["meta", "styles", "automatic-styles", "master-styles", "body"], Fodt.Sections(flat));
            Assert.Equal("Hello Jane\n", Odt.Paragraphs(Fodt.Section(flat, "body")));
            Assert.Equal("Page Jane", Odt.Paragraphs(Fodt.Section(flat, "master-styles")));
            Assert.Equal("N2", flat.Descendants(Fodt.Meta + "user-defined").Single().Value);

            // Every picture is embedded, the model's and the template's, and no link to a file of the package remains
            var frames = Fodt.Frames(flat);
            Assert.Equal(["Image1", "Plain"], frames.Select(v => v.Name));
            Assert.Equal(OtherPng, frames[0].Picture);
            Assert.Equal(Png, frames[1].Picture);
            Assert.Empty(flat.Descendants().Attributes(Odt.XLink + "href"));
            Assert.Empty(result.Findings);

            // The base64 comes in lines of 72 characters, the end tag on a line of its own, as Writer embeds a picture
            var data = flat.Descendants(Odt.Office + "binary-data").First().Value;
            Assert.DoesNotContain("\r", data);
            Assert.EndsWith("\n", data);
            Assert.All(data.Split('\n'), line => Assert.True(line.Length <= 72));
        }

        [Fact]
        public void AFlatTemplateRendersFlatWithItsLayoutAndEachStyleOnce()
        {
            var automaticStyles =
                "  <style:style style:name=\"P1\" style:family=\"paragraph\"/>\n" +
                "  <style:style style:name=\"T1\" style:family=\"text\"/>\n" +
                "  <style:style style:name=\"MP1\" style:family=\"paragraph\"/>\n" +
                "  <style:page-layout style:name=\"pm1\"/>\n";
            var body = Fodt.P("Hello <text:span text:style-name=\"T1\">", "<$Name>", "</text:span>");
            var header = Fodt.H("<text:span text:style-name=\"T1\">", "<$Name>", "</text:span>");
            var template = Fodt.Build(body, header, automaticStyles);

            var result = new OpenDocumentTemplate(template, Flat).Render(new Dictionary<string, object> { ["Name"] = "Jane" });

            var text = Encoding.UTF8.GetString(result.Document);
            var flat = Fodt.Document(result.Document);
            Assert.Equal(["meta", "settings", "scripts", "font-face-decls", "styles", "automatic-styles", "master-styles", "body"], Fodt.Sections(flat));
            Assert.Equal("Hello Jane", Odt.Paragraphs(Fodt.Section(flat, "body")));
            Assert.Equal("Jane", Odt.Paragraphs(Fodt.Section(flat, "master-styles")));

            // T1, used by the body and the header, went to both parts and comes back once; the template's indentation is kept
            Assert.Equal(["P1", "T1", "MP1", "pm1"], Fodt.AutomaticStyles(flat));
            Assert.Single(flat.Descendants(Fodt.Style + "font-face"));
            Assert.Contains("\n   <text:p text:style-name=\"P1\">Hello <text:span text:style-name=\"T1\">Jane</text:span></text:p>\n", text);
            Assert.Contains("\n <office:automatic-styles>\n  <style:style style:name=\"P1\"", text);
        }

        [Fact]
        public void TheLibreOfficeFlatTemplateRendersLikeItsPackage()
        {
            var package = new OpenDocumentTemplate<Model>(Resource("EmbeddedTemplate.odt")).Render(Model);
            var flat = new OpenDocumentTemplate<Model>(Resource("EmbeddedTemplate.fodt")).Render(Model);

            Assert.Equal(Odt.Paragraphs(Odt.Content(package.Document)), Odt.Paragraphs(Odt.Content(flat.Document)));
            Assert.Equal(Odt.Paragraphs(Odt.Styles(package.Document)), Odt.Paragraphs(Odt.Styles(flat.Document)));
            Assert.Equal(Odt.Frames(Odt.Content(package.Document)).Select(v => v.Name), Odt.Frames(Odt.Content(flat.Document)).Select(v => v.Name));
            Assert.Equal(package.Findings.Select(v => v.Text + ": " + v.Message), flat.Findings.Select(v => v.Text + ": " + v.Message));

            // The picture Writer embedded three times is one file, with the bytes of the package's picture
            var picture = Assert.Single(Odt.PictureFiles(flat.Document));
            Assert.Equal(Odt.Entry(package.Document, "Pictures/1000020100000188000000B07F83F2786D7FB2E8.png").Bytes, Odt.Entry(flat.Document, picture).Bytes);
        }

        [Fact]
        public void FilesNoElementShowsHaveNoPlaceInAFlatDocument()
        {
            var template = new OpenDocumentTemplate<Model>(Resource("EmbeddedTemplate.odt"), Flat);

            var result = template.Render(new Model());

            var text = Encoding.UTF8.GetString(result.Document);
            Assert.DoesNotContain("Thumbnails", text);
            Assert.DoesNotContain("Pictures/", text);
            var flat = Fodt.Document(result.Document);
            Assert.Equal(["meta", "settings", "scripts", "font-face-decls", "styles", "automatic-styles", "master-styles", "body"], Fodt.Sections(flat));
            Assert.NotEmpty(flat.Descendants(Odt.Draw + "image"));
            Assert.All(flat.Descendants(Odt.Draw + "image"), v => Assert.NotNull(v.Element(Odt.Office + "binary-data")));
        }

        [Fact]
        public void ALinkToSomethingOutsideThePackageStaysALink()
        {
            var package = Odt.Build("<text:p>" + Odt.Frame("Web", "https://example.com/x.png") + "</text:p>");

            var result = new OpenDocumentTemplate(package, Flat).Render(new Dictionary<string, object>());

            var image = Fodt.Document(result.Document).Descendants(Odt.Draw + "image").Single();
            Assert.Equal("https://example.com/x.png", (string)image.Attribute(Odt.XLink + "href"));
            Assert.Empty(image.Elements());
        }

        [Fact]
        public void AFlatTemplateWithoutAMediaTypeIsAText()
        {
            var template = Fodt.Build(Fodt.P("x"), mimetype: null);

            var result = new OpenDocumentTemplate(template).Render(new Dictionary<string, object>());

            Assert.Equal(Odt.MediaType, Encoding.ASCII.GetString(Odt.Entry(result.Document, "mimetype").Bytes));
            Assert.Equal(Odt.MediaType, Odt.ManifestEntries(result.Document)["/"]);
        }

        [Fact]
        public void InspectAndValidateReadAFlatTemplate()
        {
            var template = Fodt.Build(Fodt.P("<$Name>"), Fodt.H("<$Nope>"));

            var inspection = new OpenDocumentTemplate(template).Inspection;
            var validation = new OpenDocumentTemplate(template).Validate(new DictionaryShape(new Dictionary<string, IShape> { ["Name"] = DictionaryShape.Open }));

            Assert.Equal(["Name", "Nope"], inspection.Bindings.Select(v => v.Tag.Path));
            Assert.Equal(["content.xml", "styles.xml"], inspection.Bindings.Select(v => v.Location.Part));
            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("<$Nope>", finding.Text);
            Assert.Equal("styles.xml", finding.Location.Part);
        }

        [Fact]
        public void WhatIsNeitherAPackageNorAFlatDocumentIsRefused()
        {
            var text = Encoding.UTF8.GetBytes("hello");
            var content = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\"/>");

            Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(text));
            Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(Array.Empty<byte>()));
            Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(text).Inspection);
            Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(text).Validate(DictionaryShape.Open));
            var exception = Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(content));
            Assert.Contains("office:document-content, not office:document", exception.Message);
        }

        [Fact]
        public void BinaryDataThatIsNotBase64IsRefused()
        {
            var template = Encoding.UTF8.GetString(Fodt.Build(Fodt.P(Fodt.Frame("Logo", Png)))).Replace(Convert.ToBase64String(Png), "not base64!");

            var exception = Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(Encoding.UTF8.GetBytes(template)));
            Assert.StartsWith("the document embeds binary data that is not base64", exception.Message);
        }

        private static byte[] Resource(string name)
        {
            var assembly = typeof(FlatTests).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(v => v.EndsWith("." + name));
            using var resource = assembly.GetManifestResourceStream(resourceName);
            using var output = new MemoryStream();
            resource.CopyTo(output);
            return output.ToArray();
        }
    }
}
