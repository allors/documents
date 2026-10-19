// <copyright file="EscapingTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Allors.Documents;
    using Xunit;

    /// <summary>The template renders as typed, and what the model gives is escaped.</summary>
    public class EscapingTests
    {
        [Fact]
        public void StaticTextKeepsItsSpecialCharacters()
        {
            var package = Odt.Build("<text:p>Terms &amp; Conditions, if a &lt; b, Next &gt;&gt; page</text:p>");

            var rendered = Odt.Render(package);

            Assert.Equal("Terms & Conditions, if a < b, Next >> page", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void AttributeValuesKeepTheirSpecialCharacters()
        {
            var package = Odt.Build("<text:p><text:a xlink:type=\"simple\" xlink:href=\"https://example.com/?a=1&amp;b=2\" office:title=\"say &quot;hi&quot; &lt;now&gt;\">link</text:a></text:p>");

            var rendered = Odt.Render(package);

            var link = Odt.Content(rendered).Descendants(Odt.Text + "a").Single();
            Assert.Equal("https://example.com/?a=1&b=2", (string)link.Attribute(Odt.XLink + "href"));
            Assert.Equal("say \"hi\" <now>", (string)link.Attribute(Odt.Office + "title"));
        }

        [Fact]
        public void StaticTextInStylesKeepsItsSpecialCharacters()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header><text:p>Header &amp; footer &lt;3</text:p></style:header></style:master-page></office:master-styles>";
            var package = Odt.Build("<text:p>body</text:p>", masterStyles: header);

            var rendered = Odt.Render(package);

            Assert.Equal("Header & footer <3", Odt.Paragraphs(Odt.Styles(rendered)));
        }

        [Fact]
        public void BackslashesAndTheDelimitersOfEarlierEnginesInStaticTextAreKeptLiterally()
        {
            const string text = "arrows \u02C2 and \u02C3 here, path C:\\temp\\new, UNC \\\\server\\share, escaped \\\u02C2x\u02C3 and \\}";
            var package = Odt.Build("<text:p>" + text + "</text:p>");

            var rendered = Odt.Render(package);

            Assert.Equal(text, Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void BackslashesAndTheDelimitersOfEarlierEnginesInAttributeValuesAreKeptLiterally()
        {
            const string href = "file:///C:\\temp\\\u02C2x\u02C3";
            var package = Odt.Build("<text:p><text:a xlink:type=\"simple\" xlink:href=\"" + href + "\">link</text:a></text:p>");

            var rendered = Odt.Render(package);

            var link = Odt.Content(rendered).Descendants(Odt.Text + "a").Single();
            Assert.Equal(href, (string)link.Attribute(Odt.XLink + "href"));
        }

        [Fact]
        public void BoundValuesMayContainBackslashesAndTheDelimitersOfEarlierEngines()
        {
            const string name = "a \u02C2b\u02C3 c:\\d";
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Name>") + "</text:p>");
            var model = new Dictionary<string, object> { ["Name"] = name };

            var rendered = Odt.Render(package, model);

            Assert.Equal(name, Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void BoundValuesAreEscaped()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Name>") + "</text:p><text:p>" + Odt.Placeholder("<$Company>") + "</text:p>");
            var model = new Dictionary<string, object>
            {
                ["Name"] = "R&D <Ltd>",
                ["Company"] = new Company("Smith & Sons"),
            };

            var rendered = Odt.Render(package, model);

            Assert.Equal("R&D <Ltd>\nSmith & Sons", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void PlainValuesRenderInTheCultureOfTheSettings()
        {
            var package = Odt.Build(
                "<text:p>" + Odt.Placeholder("<$Flag>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$Amount>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$Day>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$Count>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<$When>") + "</text:p>");
            var model = new Dictionary<string, object>
            {
                ["Flag"] = true,
                ["Amount"] = 2.5m,
                ["Day"] = DayOfWeek.Monday,
                ["Count"] = 42,
                ["When"] = new DateTime(2026, 10, 12, 14, 30, 0),
            };

            var rendered = Odt.Render(package, model, new RenderSettings { Culture = new CultureInfo("nl-BE") });

            Assert.Equal("true\n2,5\nMonday\n42\n12/10/2026 14:30:00", Odt.Paragraphs(Odt.Content(rendered)));
        }

        private sealed class Company
        {
            private readonly string name;

            public Company(string name) => this.name = name;

            public override string ToString() => this.name;
        }
    }
}
