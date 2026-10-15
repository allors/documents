// <copyright file="WhitespaceTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    /// <summary>The engine adds no white space of its own and writes a value's white space as Writer saves typed text.</summary>
    public class WhitespaceTests
    {
        [Fact]
        public void TemplateTextRendersAsTyped()
        {
            var package = Odt.Build(
                "<text:p>Supplier<text:span text:style-name=\"T1\">:</text:span> (" + Odt.Placeholder("<$Vat>") + "%)</text:p>" +
                "<text:p>Hel<text:span text:style-name=\"T1\">lo</text:span> world</text:p>" +
                "<text:p>one<text:span text:style-name=\"T1\"> </text:span>two</text:p>");

            var rendered = Odt.Render(package, new Dictionary<string, object> { ["Vat"] = 21 });

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

            var rendered = Odt.Render(package, model);

            Assert.Equal("JohnDoe\nJohn Doe", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void TabsAndLineBreaksInValuesRenderAsTyped()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Text>") + "</text:p>");
            var model = new Dictionary<string, object> { ["Text"] = "Tab\tNew\nLine\r\nCR\rEnd" };

            var rendered = Odt.Render(package, model);

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

            var rendered = Odt.Render(package, model);

            var paragraph = Odt.Content(rendered).Descendants(Odt.Text + "p").Single();
            Assert.Equal("a  b   c ", Odt.ReaderText(paragraph));
            Assert.Equal(["1", "2"], paragraph.Elements(Odt.Text + "s").Select(v => (string)v.Attribute(Odt.Text + "c") ?? "1"));
        }

        [Fact]
        public void WhitespaceOnlyValuesAreKept()
        {
            var package = Odt.Build("<text:p>[" + Odt.Placeholder("<$Space>") + "][" + Odt.Placeholder("<$Tab>") + "]</text:p>");
            var model = new Dictionary<string, object> { ["Space"] = " ", ["Tab"] = "\t" };

            var rendered = Odt.Render(package, model);

            Assert.Equal("[ ][\t]", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void CharactersXmlCannotHoldAreDroppedAndSurrogatePairsAreKept()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Text>") + "</text:p>");
            var model = new Dictionary<string, object> { ["Text"] = "a\u0001b\u001Fc\uD800d￾e😀f" };

            var rendered = Odt.Render(package, model);

            Assert.Equal("abcde😀f", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void HeaderTextRendersAsTyped()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header><text:p>Page<text:span text:style-name=\"T1\">:</text:span> " + Odt.Placeholder("<$Number>") + "</text:p></style:header></style:master-page></office:master-styles>";
            var package = Odt.Build("<text:p>body</text:p>", masterStyles: header);

            var rendered = Odt.Render(package, new Dictionary<string, object> { ["Number"] = 7 });

            Assert.Equal("Page: 7", Odt.Paragraphs(Odt.Styles(rendered)));
        }
    }
}
