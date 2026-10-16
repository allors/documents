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
            var frames = Odt.Frame("Logo", "Pictures/placeholder.png", "<$Issuer.LogoImage empty=\"keep\">", "The issuer's logo") + Odt.Frame("Barcode", "Pictures/placeholder.png", "The barcode") + Odt.Frame("$Legacy", "Pictures/placeholder.png");
            var package = Odt.Build("<text:p>" + frames + "</text:p>", new Dictionary<string, byte[]> { ["placeholder.png"] = new byte[] { 1 } }, header);

            var inspection = OpenDocumentTemplate.Inspect(package);

            var page = Assert.Single(inspection.Tags);
            Assert.Equal("styles.xml", page.Location.Part);
            Assert.Equal("Page <$Page>", page.Location.ParagraphText);

            // A picture is bound by the tag in its description; its name is the author's, and a plain description or a $ name binds nothing
            var picture = Assert.Single(inspection.Pictures);
            Assert.Equal("Logo", picture.FrameName);
            Assert.Equal("Issuer.LogoImage", picture.Path);
            Assert.Equal(TagKind.Picture, picture.Tag.Kind);
            Assert.Equal("keep", picture.Tag.Attributes["empty"]);
            Assert.Equal("content.xml", picture.Location.Part);
            Assert.Null(picture.Block);
            Assert.Equal(["Page", "Issuer.LogoImage"], inspection.Paths);
            Assert.Empty(inspection.Problems);
        }

        [Fact]
        public void APictureInALoopKnowsItsBlock()
        {
            var package = Odt.Build(
                "<text:p>" + Odt.Placeholder("<@for item Items>") + "</text:p>" +
                "<text:p>" + Odt.Frame("Photo", "Pictures/p.png", "<$item.Photo>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@end>") + "</text:p>");

            var inspection = OpenDocumentTemplate.Inspect(package);

            var picture = Assert.Single(inspection.Pictures);
            Assert.Equal("item.Photo", picture.Path);
            Assert.Equal("item", picture.Block.Tag.Variable);
            Assert.Equal(2, picture.Location.Paragraph);

            // The description is text of the paragraph that holds the frame, so the paragraph's text shows the tag as the author typed it
            Assert.Equal("<$item.Photo> in content.xml paragraph 2 \"<$item.Photo>\"", picture.ToString());
        }

        [Fact]
        public void ADescriptionThatMeansATagButIsNotOneIsAProblem()
        {
            var package = Odt.Build(
                "<text:p>" + Odt.Frame("Logo", "Pictures/p.png", "Company logo <$Logo>") + "</text:p>" +
                "<text:p>" + Odt.Frame("Photos", "Pictures/p.png", "<@for p Photos>") + "</text:p>" +
                "<text:p><draw:frame draw:name=\"Box\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"1cm\"><draw:text-box><text:p>box</text:p></draw:text-box><svg:desc>&lt;$Logo&gt;</svg:desc></draw:frame></text:p>" +
                "<text:p>" + Odt.Frame("Note", "Pictures/p.png", "Just a note, no tag") + "</text:p>");

            var inspection = OpenDocumentTemplate.Inspect(package);

            Assert.Empty(inspection.Pictures);
            Assert.Equal(3, inspection.Problems.Count);
            Assert.Equal("Company logo <$Logo>", inspection.Problems[0].Text);
            Assert.Contains("whole description", inspection.Problems[0].Message);
            Assert.Equal("<@for p Photos>", inspection.Problems[1].Text);
            Assert.Contains("not a block tag", inspection.Problems[1].Message);
            Assert.Equal("<$Logo>", inspection.Problems[2].Text);
            Assert.Contains("holds no picture", inspection.Problems[2].Message);
            Assert.Equal(3, inspection.Problems[2].Location.Paragraph);
            Assert.All(inspection.Problems, v => Assert.False(v.IsUnbalanced));
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
                "<text:p>" + Odt.Frame("Photo", "Pictures/p.png", "<$item.Photo[0]>") + "</text:p>");

            var inspection = OpenDocumentTemplate.Inspect(package);

            Assert.Equal(4, inspection.Problems.Count);
            Assert.Equal("<$a + b>", inspection.Problems[0].Text);
            Assert.Equal(1, inspection.Problems[0].Location.Paragraph);
            Assert.Equal("<@end>", inspection.Problems[1].Text);
            Assert.Contains("closes nothing", inspection.Problems[1].Message);
            Assert.Equal("<$item.Photo[0]>", inspection.Problems[2].Text);
            Assert.Equal(4, inspection.Problems[2].Location.Paragraph);
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
            Assert.All(inspection.Tags, v => Assert.True(v.Location.Paragraph > 0));

            // The template names its frame $image, which bound a picture in 2.x; a picture is bound by its description now
            Assert.Empty(inspection.Pictures);
        }

        [Fact]
        public void ElseTagsAreChildrenOfTheirCondition()
        {
            var package = Odt.Build(
                "<text:p>" + Odt.Placeholder("<@ifdef Receiver.FirstName>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$Receiver.FirstName>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@else if Receiver.Name>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$Receiver.Name>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@else>") + "</text:p>" +
                "<text:p>" + Odt.Frame("Logo", "Pictures/p.png", "<$Logo>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@end>") + "</text:p>");

            var inspection = OpenDocumentTemplate.Inspect(package);

            Assert.Empty(inspection.Problems);
            var condition = Assert.Single(inspection.Tags);
            Assert.Equal(["<$Receiver.FirstName>", "<@else if Receiver.Name>", "<$Receiver.Name>", "<@else>", "<@end>"], condition.Children.Select(v => v.Tag.Text));
            Assert.Equal(["<@else if Receiver.Name>", "<@else>"], condition.Elses.Select(v => v.Tag.Text));
            Assert.All(condition.Children, v => Assert.Same(condition, v.Block));
            Assert.Single(inspection.Blocks);
            Assert.Equal(["Receiver.FirstName", "Receiver.Name", "Logo"], inspection.Paths);
            Assert.Same(condition, Assert.Single(inspection.Pictures).Block);
        }

        [Fact]
        public void MapsAreInspectedWithTheirRows()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + Odt.Map("Titles", "true|CREDIT NOTE", "false|INVOICE") + "</style:header></style:master-page></office:master-styles>";
            var package = Odt.Build("<text:p>x</text:p>" + Odt.Map("Labels", "Payment|PAYMENT", "default|OTHER") + "<text:p>" + Odt.Placeholder("<$Kind map=\"Labels\">") + "</text:p>", masterStyles: header);

            var inspection = OpenDocumentTemplate.Inspect(package);

            Assert.Empty(inspection.Problems);
            Assert.Equal(["Labels", "Titles"], inspection.Maps.Select(v => v.Name));

            var labels = inspection.Maps[0];
            Assert.Equal(TagKind.Map, labels.Tag.Kind);
            Assert.Equal("<@map Labels>", labels.Tag.Text);
            Assert.Equal(["Payment"], labels.Entries.Keys);
            Assert.Equal("PAYMENT", labels.Entries["Payment"]);
            Assert.True(labels.HasDefault);
            Assert.Equal("OTHER", labels.Default);
            Assert.Equal("content.xml", labels.Location.Part);
            Assert.Equal(2, labels.Location.Paragraph);
            Assert.Equal("<@map Labels> in content.xml paragraph 2 \"<@map Labels>\"", labels.ToString());
            Assert.True(labels.TryGet("Payment", out var text) && text == "PAYMENT");
            Assert.True(labels.TryGet("Other", out text) && text == "OTHER");

            var titles = inspection.Maps[1];
            Assert.Equal("styles.xml", titles.Location.Part);
            Assert.False(titles.HasDefault);
            Assert.Null(titles.Default);
            Assert.False(titles.TryGet("maybe", out _));

            // The binding after the map sits in paragraph 8: the map's six paragraphs count
            Assert.Equal(8, Assert.Single(inspection.Tags).Location.Paragraph);
            Assert.Equal(["Kind"], inspection.Paths);
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
