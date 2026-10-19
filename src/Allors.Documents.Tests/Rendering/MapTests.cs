// <copyright file="MapTests.cs" company="Allors bvba">
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
    using static Allors.Documents.Tests.Render;

    /// <summary>Maps: a two-column table the template keeps for itself, through which a binding prints the text for its value.</summary>
    public class MapTests
    {
        [Fact]
        public void AValuePrintsTheTextItsMapGivesAndTheTableLeavesTheDocument()
        {
            var body = Odt.Map("Titles", "true|CREDIT NOTE", "false|INVOICE") + P("<$IsCreditNote map=\"Titles\">");

            var result = Document(body, new Dictionary<string, object> { ["IsCreditNote"] = false });

            Assert.Equal("INVOICE", Paragraphs(result));
            Assert.Empty(Odt.Content(result.Document).Descendants(Odt.Table + "table"));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void AMapServesItsDefaultForAValueItHasNoRowFor()
        {
            var body = Odt.Map("Labels", "Payment|PAYMENT", "default|OTHER TERMS") + P("<$Kind map='Labels'>");

            var result = Document(body, new Dictionary<string, object> { ["Kind"] = "Delivery" });

            Assert.Equal("OTHER TERMS", Paragraphs(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void AMapWithoutADefaultLetsTheValuePrintAndReportsOncePerKey()
        {
            var body = Odt.Map("Labels", "a|A") + P("<@for k Keys>") + P("<$k map=\"Labels\">") + P("<@end>");

            var result = Document(body, new Dictionary<string, object> { ["Keys"] = new[] { "b", "a", "b" } });

            Assert.Equal("b\nA\nb", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.MissingKey, finding.Kind);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Equal("k", finding.Path);
            Assert.Equal("the map 'Labels' has no row for 'b' and no default row: the value prints as it is", finding.Message);
        }

        [Fact]
        public void TheKeyIsTheValueInItsInvariantText()
        {
            var body = Odt.Map("Words", "2.5|two and a half", "true|yes", "Friday|vrijdag", "3|three") + Tags("<$Amount map=\"Words\">", "<$Flag map=\"Words\">", "<$Day map=\"Words\">", "<$Count map=\"Words\">");
            var model = new Dictionary<string, object> { ["Amount"] = 2.5m, ["Flag"] = true, ["Day"] = DayOfWeek.Friday, ["Count"] = 3 };
            var settings = new RenderSettings { Culture = new CultureInfo("nl-BE"), Number = "N2", True = "Ja" };

            Assert.Equal("two and a half yes vrijdag three", Text(body, model, settings));
        }

        [Fact]
        public void ANullValuePrintsBlankWithoutALookup()
        {
            var body = Odt.Map("Words", "default|other") + P("[", "<$Nothing map=\"Words\">", "]");

            var result = Document(body, new Dictionary<string, object> { ["Nothing"] = null });

            Assert.Equal("[]", Paragraphs(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void TheOtherAttributesApplyWhenTheValuePrintsAsItIs()
        {
            var body = Odt.Map("Words", "1|one") + Tags("<$A map=\"Words\" format=\"N2\" culture=\"nl-BE\">", "<$B map=\"Words\" format=\"N2\">");

            var result = Document(body, new Dictionary<string, object> { ["A"] = 2.5m, ["B"] = 1 });

            Assert.Equal("2,50 one", Paragraphs(result));
            Assert.Single(result.Findings);
        }

        [Fact]
        public void AMapInTheStylesPartServesTheBodyAndTheOtherWayRound()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + Odt.Map("Titles", "true|PAID") + P("<$Paid map=\"Words\">") + "</style:header></style:master-page></office:master-styles>";
            var body = Odt.Map("Words", "true|yes") + P("<$Paid map=\"Titles\">");

            var result = Document(body, new Dictionary<string, object> { ["Paid"] = true }, styles: header);

            Assert.Equal("PAID", Paragraphs(result));
            Assert.Equal("yes", Odt.Paragraphs(Odt.Styles(result.Document)));
            Assert.Empty(Odt.Styles(result.Document).Descendants(Odt.Table + "table"));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ANameThatIsNoMapIsReportedAndTheValuePrintsAsItIs()
        {
            var result = Document(P("<$Kind map=\"Nope\">"), new Dictionary<string, object> { ["Kind"] = "x" });

            Assert.Equal("x", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.UnknownMap, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("no map 'Nope' in the template: a map is a table whose first cell holds <@map Nope>", finding.Message);
        }

        [Fact]
        public void AMapTextKeepsItsLineBreaksTabsAndSpaces()
        {
            var map = "<table:table table:name=\"M\"><table:table-row><table:table-cell>" + P("<@map M>") + "</table:table-cell><table:table-cell><text:p/></table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p> k </text:p></table:table-cell><table:table-cell><text:p>a<text:line-break/>b<text:tab/>c<text:s text:c=\"2\"/>d</text:p><text:p>second</text:p></table:table-cell></table:table-row></table:table>";

            var text = Text(map + P("<$K map=\"M\">"), new Dictionary<string, object> { ["K"] = "k" });

            Assert.Equal("a\nb\tc  d\nsecond", text);
        }

        [Fact]
        public void AMapTableThatCannotBeReadIsReportedRowByRow()
        {
            var map = "<table:table table:name=\"M\"><table:table-row><table:table-cell>" + P("<@map M>") + "</table:table-cell><table:table-cell><text:p>Text</text:p></table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p>lonely</text:p></table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p>a</text:p></table:table-cell><table:table-cell>" + P("<$Tag>") + "</table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p/></table:table-cell><table:table-cell><text:p>no key</text:p></table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p>b</text:p></table:table-cell><table:table-cell><text:p>B</text:p></table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p>b</text:p></table:table-cell><table:table-cell><text:p>B2</text:p></table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p>Default</text:p></table:table-cell><table:table-cell><text:p>D</text:p></table:table-cell></table:table-row>" +
                      "<table:table-row><table:table-cell><text:p>default</text:p></table:table-cell><table:table-cell><text:p>D2</text:p></table:table-cell></table:table-row>" +
                      "</table:table>";

            var inspection = new OpenDocumentTemplate(Odt.Build(map)).Inspection;

            var inspected = Assert.Single(inspection.Maps);
            Assert.Equal("M", inspected.Name);
            Assert.Equal(new[] { "b" }, inspected.Entries.Keys);
            Assert.Equal("B", inspected.Entries["b"]);
            Assert.True(inspected.HasDefault);
            Assert.Equal("D", inspected.Default);
            Assert.Equal(1, inspected.Location.Paragraph);

            Assert.Equal(5, inspection.Problems.Count);
            Assert.All(inspection.Problems, v => Assert.Equal("<@map M>", v.Text));
            Assert.All(inspection.Problems, v => Assert.False(v.IsUnbalanced));
            Assert.Equal("a row of the map 'M' has one cell: a row holds a key in its first cell and a text in its second", inspection.Problems[0].Message);
            Assert.Equal(3, inspection.Problems[0].Location.Paragraph);
            Assert.Equal("the map 'M' holds the tag '<$Tag>': the keys and texts of a map are plain text", inspection.Problems[1].Message);
            Assert.Equal("a row of the map 'M' has no key: the first cell is empty", inspection.Problems[2].Message);
            Assert.Equal("the map 'M' has two rows for 'b': the first counts", inspection.Problems[3].Message);
            Assert.Equal("the map 'M' has two default rows: the first counts", inspection.Problems[4].Message);

            // The problems are findings of every render, and the map still serves its good rows
            var result = Document(map + P("<$K map=\"M\">"), new Dictionary<string, object> { ["K"] = "b" });
            Assert.Equal("B", Paragraphs(result));
            Assert.Equal(5, result.Findings.Count);
            Assert.All(result.Findings, v => Assert.Equal(FindingKind.MalformedTag, v.Kind));
        }

        [Fact]
        public void ASecondTableWithTheSameNameIsAProblemAndStaysInTheDocument()
        {
            var body = Odt.Map("M", "a|first") + Odt.Map("M", "a|second") + P("<$K map=\"M\">");

            var result = Document(body, new Dictionary<string, object> { ["K"] = "a" });

            Assert.Equal("<@map M>\nText\na\nsecond\nfirst", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.MalformedTag, finding.Kind);
            Assert.Equal("the map 'M' is defined twice: the first table counts", finding.Message);
        }

        [Fact]
        public void AMapTagOutsideTheFirstCellOfATableIsAMalformedTag()
        {
            var result = Document(P("<@map M>") + Table(Row(P("x"), P("<@map N>"))), new Dictionary<string, object>());

            Assert.Equal("<@map M>\nx\n<@map N>", Paragraphs(result));
            Assert.Equal(2, result.Findings.Count);
            Assert.All(result.Findings, v => Assert.Equal(FindingKind.MalformedTag, v.Kind));
            Assert.Contains("goes in the first cell of a table of two columns", result.Findings[0].Message);
        }

        [Fact]
        public void TheParagraphsOfAMapCountInTheLocations()
        {
            var body = Odt.Map("M", "a|A") + P("<$Nope>");

            var result = Document(body, new Dictionary<string, object>());

            // The map's four paragraphs come first: the tag, the heading, the key and the text
            Assert.Equal(5, Assert.Single(result.Findings).Location.Paragraph);
        }

        private static string Tags(params string[] tags) => "<text:p>" + string.Join(" ", tags.Select(Odt.Placeholder)) + "</text:p>";
    }
}
