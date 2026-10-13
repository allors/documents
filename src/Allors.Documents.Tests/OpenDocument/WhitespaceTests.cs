// <copyright file="WhitespaceTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Document.OpenDocument;
    using Xunit;

    public class WhitespaceTests
    {
        [Fact]
        public void TemplateTextRendersAsTyped()
        {
            var package = Odt.Build(
                "<text:p>Supplier<text:span text:style-name=\"T1\">:</text:span> (" + Odt.Placeholder("<$Vat>") + "%)</text:p>" +
                "<text:p>Hel<text:span text:style-name=\"T1\">lo</text:span> world</text:p>" +
                "<text:p>one<text:span text:style-name=\"T1\"> </text:span>two</text:p>");

            var rendered = new OpenDocumentTemplate(package, "Vat").Render(new Dictionary<string, object> { ["Vat"] = 21 });

            var content = Odt.Content(rendered);
            Assert.Equal("Supplier: (21%)\nHello world\none two", Odt.Paragraphs(content));
            Assert.Equal(" ", content.Descendants(Odt.Text + "span").Last().Value);
        }

        [Fact]
        public void AdjacentBindingsRenderWithoutASpace()
        {
            var package = Odt.Build(
                "<text:p>" + Odt.Placeholder("<$First>") + Odt.Placeholder("<$Last>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$First>") + " " + Odt.Placeholder("<$Last>") + "</text:p>");
            var model = new Dictionary<string, object> { ["First"] = "John", ["Last"] = "Doe" };

            var rendered = new OpenDocumentTemplate(package, "First,Last").Render(model);

            Assert.Equal("JohnDoe\nJohn Doe", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void TabsAndLineBreaksInValuesRenderAsTyped()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Text>") + "</text:p>");
            var model = new Dictionary<string, object> { ["Text"] = "Tab\tNew\nLine\r\nCR\rEnd" };

            var rendered = new OpenDocumentTemplate(package, "Text").Render(model);

            var paragraph = Odt.Content(rendered).Descendants(Odt.Text + "p").Single();
            Assert.Equal("Tab\tNew\nLine\nCR\nEnd", Odt.ReaderText(paragraph));
            Assert.Single(paragraph.Elements(Odt.Text + "tab"));
            Assert.Equal(3, paragraph.Elements(Odt.Text + "line-break").Count());
            Assert.DoesNotContain(paragraph.Value, v => v < ' ');
        }

        [Fact]
        public void RunsOfSpacesInValuesRenderAsTyped()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Text>") + "</text:p>");
            var model = new Dictionary<string, object> { ["Text"] = "a  b   c " };

            var rendered = new OpenDocumentTemplate(package, "Text").Render(model);

            var paragraph = Odt.Content(rendered).Descendants(Odt.Text + "p").Single();
            Assert.Equal("a  b   c ", Odt.ReaderText(paragraph));
            Assert.Equal(["1", "2"], paragraph.Elements(Odt.Text + "s").Select(v => (string)v.Attribute(Odt.Text + "c") ?? "1"));
        }

        [Fact]
        public void WhitespaceOnlyValuesAreKept()
        {
            var package = Odt.Build("<text:p>[" + Odt.Placeholder("<$Space>") + "][" + Odt.Placeholder("<$Tab>") + "]</text:p>");
            var model = new Dictionary<string, object> { ["Space"] = " ", ["Tab"] = "\t" };

            var rendered = new OpenDocumentTemplate(package, "Space,Tab").Render(model);

            Assert.Equal("[ ][\t]", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void OtherControlCharactersAreDropped()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Text>") + "</text:p>");
            var model = new Dictionary<string, object> { ["Text"] = "a\u0001b\u001Fc" };

            var rendered = new OpenDocumentTemplate(package, "Text").Render(model);

            Assert.Equal("abc", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void HeaderTextRendersAsTyped()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header><text:p>Page<text:span text:style-name=\"T1\">:</text:span> " + Odt.Placeholder("<$Number>") + "</text:p></style:header></style:master-page></office:master-styles>";
            var package = Odt.Build("<text:p>body</text:p>", masterStyles: header);

            var rendered = new OpenDocumentTemplate(package, "Number").Render(new Dictionary<string, object> { ["Number"] = 7 });

            Assert.Equal("Page: 7", Odt.Paragraphs(Odt.Styles(rendered)));
        }

        [Fact]
        public void PictureNamesRenderAsAttributeValues()
        {
            var placeholder = new byte[] { 1, 2, 3 };
            var package = Odt.Build("<text:p>" + Odt.Frame("$Logo", "Pictures/placeholder.png") + "</text:p>", new Dictionary<string, byte[]> { ["placeholder.png"] = placeholder });
            var model = new Dictionary<string, object> { ["Logo"] = "say \"hi\"\tnow" };

            var rendered = new OpenDocumentTemplate(package, "Logo").Render(model);

            var frame = Odt.Content(rendered).Descendants(Odt.Draw + "frame").Single();
            Assert.Equal("say \"hi\"now", (string)frame.Attribute(Odt.Draw + "name"));
        }

        [Fact]
        public void TypedSpacesSurviveThePictureBinding()
        {
            var placeholder = new byte[] { 1, 2, 3 };
            var logo = new byte[] { 4, 5, 6 };
            var package = Odt.Build(
                "<text:p>one<text:span text:style-name=\"T1\"> </text:span>two</text:p>" +
                "<text:p>" + Odt.Frame("$Logo", "Pictures/placeholder.png") + "</text:p>",
                new Dictionary<string, byte[]> { ["placeholder.png"] = placeholder });

            var rendered = new OpenDocumentTemplate(package, "Logo").Render(new Dictionary<string, object> { ["Logo"] = "logo" }, new Dictionary<string, byte[]> { ["logo"] = logo });

            Assert.Equal("one two\n", Odt.Paragraphs(Odt.Content(rendered)));
        }
    }
}
