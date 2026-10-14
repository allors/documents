// <copyright file="InspectionTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Inspection.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Allors.Document.OpenDocument;
    using Allors.Document.Tags;
    using Allos.Document.OpenDocument.Tests;
    using Xunit;

    public class InspectionTests
    {
        [Fact]
        public void TagsComeInDocumentOrderWithTheirParagraph()
        {
            var package = Odt.Build(
                "<text:p>Dear " + Odt.Placeholder("<$Receiver.Name>") + ",</text:p>" +
                "<text:p>" + Odt.Placeholder("<@for line Lines>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$line.Description>") + " " + Odt.Placeholder("<$line.Amount format=\"N2\">") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@end>") + "</text:p>");

            var inspection = OpenDocumentTemplate.Inspect(package);

            Assert.Empty(inspection.Problems);
            Assert.Equal(["<$Receiver.Name>", "<@for line Lines>"], inspection.Tags.Select(v => v.Tag.Text));

            var greeting = inspection.Tags[0];
            Assert.Equal("content.xml", greeting.Location.Part);
            Assert.Equal(1, greeting.Location.Paragraph);
            Assert.Equal("Dear <$Receiver.Name>,", greeting.Location.ParagraphText);
            Assert.Null(greeting.Block);

            var loop = inspection.Tags[1];
            Assert.Equal(["<$line.Description>", "<$line.Amount format=\"N2\">", "<@end>"], loop.Children.Select(v => v.Tag.Text));
            Assert.Equal(3, loop.Children[0].Location.Paragraph);
            Assert.Same(loop, loop.Children[0].Block);
            Assert.Equal(["line"], loop.Children[0].Variables);
            Assert.Equal("N2", loop.Children[1].Tag.Attributes["format"]);
            Assert.Equal(["Receiver.Name", "Lines", "line.Description", "line.Amount"], inspection.Paths);
        }

        [Fact]
        public void HeadersAndPicturesAreInspectedToo()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header><text:p>Page " + Odt.Placeholder("<$Page>") + "</text:p></style:header></style:master-page></office:master-styles>";
            var package = Odt.Build("<text:p>" + Odt.Frame("$Issuer.LogoImage", "Pictures/placeholder.png") + Odt.Frame("Barcode", "Pictures/placeholder.png") + "</text:p>", new Dictionary<string, byte[]> { ["placeholder.png"] = new byte[] { 1 } }, header);

            var inspection = OpenDocumentTemplate.Inspect(package);

            var page = Assert.Single(inspection.Tags);
            Assert.Equal("styles.xml", page.Location.Part);
            Assert.Equal("Page <$Page>", page.Location.ParagraphText);

            var picture = Assert.Single(inspection.Pictures);
            Assert.Equal("$Issuer.LogoImage", picture.FrameName);
            Assert.Equal("Issuer.LogoImage", picture.Path);
            Assert.Equal("content.xml", picture.Location.Part);
        }

        [Fact]
        public void DocumentSettingsComeFromTheCustomProperties()
        {
            var package = Odt.Build("<text:p>body</text:p>", settings: new Dictionary<string, string> { ["culture"] = "nl-BE", ["date"] = "dd-MMM-yyyy" });

            var inspection = OpenDocumentTemplate.Inspect(package);

            Assert.Equal("nl-BE", inspection.Settings["culture"]);
            Assert.Equal("dd-MMM-yyyy", inspection.Settings["date"]);
        }

        [Fact]
        public void ProblemsNameTheTagAndItsParagraph()
        {
            var package = Odt.Build(
                "<text:p>" + Odt.Placeholder("<$a + b>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@end>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@if Receiver.Contact>") + "</text:p>" +
                "<text:p>" + Odt.Frame("$item.Photo[0]", "Pictures/p.png") + "</text:p>");

            var inspection = OpenDocumentTemplate.Inspect(package);

            Assert.Equal(4, inspection.Problems.Count);
            Assert.Equal("<$a + b>", inspection.Problems[0].Text);
            Assert.Equal(1, inspection.Problems[0].Location.Paragraph);
            Assert.Equal("<@end>", inspection.Problems[1].Text);
            Assert.Contains("closes nothing", inspection.Problems[1].Message);
            Assert.Equal("$item.Photo[0]", inspection.Problems[2].Text);
            Assert.Equal("<@if Receiver.Contact>", inspection.Problems[3].Text);
            Assert.Contains("never ends", inspection.Problems[3].Message);
        }

        [Fact]
        public void TheWriterTemplateOfTheSuiteIsInspected()
        {
            var inspection = OpenDocumentTemplate.Inspect(Resource("EmbeddedTemplate.odt"));

            Assert.Empty(inspection.Problems);
            Assert.Contains("Person.FirstName", inspection.Paths);
            Assert.Contains("People", inspection.Paths);
            Assert.Contains("Images", inspection.Paths);
            Assert.Contains(inspection.Blocks, v => v.Tag.Kind == TagKind.For && v.Tag.Variable == "p" && v.Children.Any(c => c.Tag.Kind == TagKind.For && c.Tag.Variable == "p2"));
            Assert.Contains(inspection.Pictures, v => v.Path == "image" && v.Block?.Tag.Variable == "image");
            Assert.All(inspection.Tags, v => Assert.True(v.Location.Paragraph > 0));
        }

        private static byte[] Resource(string name)
        {
            var assembly = typeof(InspectionTests).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(v => v.EndsWith("." + name));
            using var resource = assembly.GetManifestResourceStream(resourceName);
            using var output = new MemoryStream();
            resource.CopyTo(output);
            return output.ToArray();
        }
    }
}
