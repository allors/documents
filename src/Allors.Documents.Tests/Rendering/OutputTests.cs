// <copyright file="OutputTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Allors.Document.OpenDocument;
    using Allors.Document.Reflection;
    using Allos.Document.OpenDocument.Tests;
    using Xunit;
    using static Allos.Document.Rendering.Tests.Render;

    /// <summary>What the resolver engine writes around the values: the parts as the template had them, the package, and the other files.</summary>
    public class OutputTests
    {
        [Fact]
        public void APartStartsWithTheDeclarationAndHasNoByteOrderMark()
        {
            var result = Document(P("x"), new Dictionary<string, object>());

            var content = Odt.Entry(result.Document, "content.xml").Bytes;
            var styles = Odt.Entry(result.Document, "styles.xml").Bytes;
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<office:document-content ", Encoding.UTF8.GetString(content));
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<office:document-styles ", Encoding.UTF8.GetString(styles));
            Assert.Equal((byte)'<', content[0]);
        }

        [Fact]
        public void TemplateElementsGetFullEndTagsAndValueElementsAreSelfClosing()
        {
            var result = Document(P("<$Text>"), new Dictionary<string, object> { ["Text"] = "a  b\tc" });

            var content = Encoding.UTF8.GetString(Odt.Entry(result.Document, "content.xml").Bytes);
            var styles = Encoding.UTF8.GetString(Odt.Entry(result.Document, "styles.xml").Bytes);
            Assert.Contains("<text:p>a <text:s />b<text:tab />c</text:p>", content);
            Assert.Contains("<office:styles></office:styles>", styles);
        }

        [Fact]
        public void TheRootElementKeepsItsNamespaceDeclarationsInOrder()
        {
            var package = Odt.Build(P("x"));
            var template = Encoding.UTF8.GetString(Odt.Entry(package, "content.xml").Bytes);

            var result = new OpenDocumentTemplate(package, string.Empty).Render(new Dictionary<string, object>(), new RenderSettings());

            var rendered = Encoding.UTF8.GetString(Odt.Entry(result.Document, "content.xml").Bytes);
            Assert.Equal(StartTag(template, "office:document-content"), StartTag(rendered, "office:document-content"));
        }

        [Fact]
        public void CommentsAndProcessingInstructionsPassThrough()
        {
            var result = Document("<!-- a note --><?draft yes?>" + P("x"), new Dictionary<string, object>());

            var text = Odt.Content(result.Document).Descendants(Odt.Office + "text").Single();
            Assert.Equal(" a note ", text.Nodes().OfType<XComment>().Single().Value);
            Assert.Equal("draft", text.Nodes().OfType<XProcessingInstruction>().Single().Target);
        }

        [Fact]
        public void PlainPictureFramesPassThroughUntouched()
        {
            var placeholder = new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3 };
            var pictures = new Dictionary<string, byte[]> { ["placeholder.png"] = placeholder };
            var package = Odt.Build(P(Odt.Frame("Logo", "Pictures/placeholder.png", "The company logo") + Odt.Frame("$Logo", "Pictures/placeholder.png")), pictures);
            var model = new Dictionary<string, object> { ["Logo"] = new byte[] { 9, 9, 9 } };

            var result = new OpenDocumentTemplate(package, string.Empty).Render(model, new RenderSettings());

            // A frame binds by the tag in its description; a plain description or a name starting with $ binds nothing under this engine
            Assert.Equal([("Logo", "Pictures/placeholder.png"), ("$Logo", "Pictures/placeholder.png")], Odt.Frames(Odt.Content(result.Document)));
            Assert.Equal("The company logo", Odt.Content(result.Document).Descendants(Odt.Svg + "desc").Single().Value);
            Assert.Equal(placeholder, Odt.Entry(result.Document, "Pictures/placeholder.png").Bytes);
            Assert.True(Odt.Entry(result.Document, "Pictures/placeholder.png").Stored);
            Assert.Equal(["Pictures/placeholder.png"], Odt.PictureFiles(result.Document));
            Assert.Equal(Odt.Entry(package, "META-INF/manifest.xml").Bytes, Odt.Entry(result.Document, "META-INF/manifest.xml").Bytes);
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void HeadersRenderWithTheSameModelAndReportInTheirPart()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P("Page ", "<$Number>", "<$Nope>") + "</style:header></style:master-page></office:master-styles>";

            var result = Document(P("body ", "<$Number>"), new Dictionary<string, object> { ["Number"] = 7 }, styles: header);

            Assert.Equal("body 7", Paragraphs(result));
            Assert.Equal("Page 7", Odt.Paragraphs(Odt.Styles(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal("styles.xml", finding.Location.Part);
            Assert.Equal("<$Nope>", finding.Text);
        }

        [Fact]
        public void ThePackageStartsWithTheMimetypeAndKeepsTheOtherFiles()
        {
            var template = new OpenDocumentTemplate(Resource("EmbeddedTemplate.odt"), string.Empty);

            var result = template.Render(new Model(), new RenderSettings { Resolver = ReflectionResolver.Instance });

            var entries = Odt.Entries(result.Document);
            Assert.Equal("mimetype", entries[0].Name);
            Assert.True(entries[0].Stored);
            Assert.Equal(["content.xml", "styles.xml", "META-INF/manifest.xml"], entries.Skip(1).Take(3).Select(v => v.Name));
            Assert.True(Odt.Entry(result.Document, "Pictures/1000020100000188000000B07F83F2786D7FB2E8.png").Stored);
            Assert.True(Odt.Entry(result.Document, "Thumbnails/thumbnail.png").Stored);
            Assert.False(Odt.Entry(result.Document, "settings.xml").Stored);
            Assert.Contains(entries, v => v.Name == "meta.xml");
            Assert.Contains(entries, v => v.Name == "manifest.rdf");
        }

        [Fact]
        public void TheLibreOfficeTemplateRendersThroughTheResolver()
        {
            var model = new Model
            {
                Person = new ModelPerson { FirstName = "Jane" },
                People = [new ModelPerson { FirstName = "John" }, new ModelPerson { FirstName = "Jenny" }],
                Images = ["number1", "number2", "number3"],
            };
            var template = new OpenDocumentTemplate(Resource("EmbeddedTemplate.odt"), string.Empty);

            var result = template.Render(model, new RenderSettings { Resolver = ReflectionResolver.Instance });

            var paragraphs = Paragraphs(result);
            Assert.Contains("Jane", paragraphs);
            Assert.Contains("John", paragraphs);
            Assert.Contains("Jenny", paragraphs);

            // The template asks for a last name the model does not have: StringTemplate printed it blank in silence
            var finding = Assert.Single(result.Findings);
            Assert.Equal("<$p.LastName>", finding.Text);
            Assert.Equal("p has no member LastName", finding.Message);

            // The frame named $image is a plain frame under this engine; it repeats with the loop, and frame names are unique in a document
            Assert.Equal(["$image", "$image 2", "$image 3"], Odt.Frames(Odt.Content(result.Document)).Select(v => v.Name).Where(v => v.StartsWith("$image")));
        }

        [Fact]
        public void BothEnginesRenderTheSameParagraphsOfTheLibreOfficeTemplate()
        {
            var model = new Model
            {
                Person = new ModelPerson { FirstName = "Jane" },
                People = [new ModelPerson { FirstName = "John" }, new ModelPerson { FirstName = "Jenny" }],
                Images = ["number1", "number2", "number3"],
            };
            var package = Resource("EmbeddedTemplate.odt");

            var stringTemplate = new OpenDocumentTemplate<Model>(package).Render(model);
            var resolver = new OpenDocumentTemplate(package, string.Empty).Render(model, new RenderSettings { Resolver = ReflectionResolver.Instance }).Document;

            Assert.Equal(Odt.Paragraphs(Odt.Content(stringTemplate)), Odt.Paragraphs(Odt.Content(resolver)));
            Assert.Equal(Odt.Paragraphs(Odt.Styles(stringTemplate)), Odt.Paragraphs(Odt.Styles(resolver)));
        }

        [Fact]
        public void ATemplateRendersRepeatedlyAndConcurrentlyToTheSameDocument()
        {
            var model = new Dictionary<string, object> { ["Items"] = new object[] { Item("a"), Item("b") }, ["Name"] = "x" };
            var template = Template(P("<$Name>") + P("<@for item Items index=\"n\">") + P("<$n>", " ", "<$item.Name>") + P("<@end>"));
            var expected = Odt.Entry(template.Render(model, new RenderSettings()).Document, "content.xml").Bytes;

            var contents = new byte[16][];
            Parallel.For(0, contents.Length, i => contents[i] = Odt.Entry(template.Render(model, new RenderSettings()).Document, "content.xml").Bytes);

            Assert.All(contents, v => Assert.Equal(expected, v));
        }

        private static string StartTag(string xml, string element)
        {
            var start = xml.IndexOf("<" + element, System.StringComparison.Ordinal);
            return xml.Substring(start, xml.IndexOf('>', start) - start + 1);
        }

        private static byte[] Resource(string name)
        {
            var assembly = typeof(OutputTests).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(v => v.EndsWith("." + name));
            using var resource = assembly.GetManifestResourceStream(resourceName);
            using var output = new MemoryStream();
            resource.CopyTo(output);
            return output.ToArray();
        }
    }
}
