// <copyright file="BlockTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Documents;
    using Xunit;
    using static Allors.Documents.Tests.Render;

    /// <summary>Conditions and loops under the resolver engine: what keeps a block, what repeats it, what the cut costs, and what cannot render.</summary>
    public class BlockTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData(false, false)]
        [InlineData("", false)]
        [InlineData("  ", false)]
        [InlineData(0, false)]
        [InlineData(0.0, false)]
        [InlineData("x", true)]
        [InlineData(true, true)]
        [InlineData(1, true)]
        [InlineData(-1.5, true)]
        public void AnIfKeepsItsBlockForATrueValue(object value, bool kept)
        {
            var text = Text(P("<@if Flag>") + P("kept") + P("<@end>") + P("after"), new Dictionary<string, object> { ["Flag"] = value });

            Assert.Equal(kept ? "kept\nafter" : "after", text);
        }

        [Fact]
        public void AnIfOnACollectionKeepsItsBlockWhenThereAreItems()
        {
            var body = P("<@if Lines>") + P("kept") + P("<@end>") + P("after");

            Assert.Equal("after", Text(body, new Dictionary<string, object> { ["Lines"] = new object[0] }));
            Assert.Equal("kept\nafter", Text(body, new Dictionary<string, object> { ["Lines"] = new object[] { 1 } }));
        }

        [Fact]
        public void AnIfOnAnUnknownMemberDropsTheBlockAndReportsIt()
        {
            var result = Document(P("<@if Nope>") + P("kept") + P("<@end>") + P("after"), new Dictionary<string, object>());

            Assert.Equal("after", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("<@if Nope>", finding.Text);
        }

        [Fact]
        public void AnIfDefKeepsTheBlockWhenTheMemberExistsWhateverItsValue()
        {
            var body = P("<@ifdef Customer.Name>") + P("Name: ", "<$Customer.Name>") + P("<@end>") + P("after");

            Assert.Equal("Name: John\nafter", Text(body, new Dictionary<string, object> { ["Customer"] = new Dictionary<string, object> { ["Name"] = "John" } }));
            Assert.Equal("Name: \nafter", Text(body, new Dictionary<string, object> { ["Customer"] = new Dictionary<string, object> { ["Name"] = null } }));
        }

        [Fact]
        public void AnIfDefDropsTheBlockForAMissingMemberWithoutAFinding()
        {
            var body = P("<@ifdef Customer.Name>") + P("kept") + P("<@end>") + P("after");

            var missing = Document(body, new Dictionary<string, object> { ["Customer"] = new Dictionary<string, object>() });
            Assert.Equal("after", Paragraphs(missing));
            Assert.Empty(missing.Findings);

            var nullBefore = Document(body, new Dictionary<string, object> { ["Customer"] = null });
            Assert.Equal("after", Paragraphs(nullBefore));
            Assert.Empty(nullBefore.Findings);
        }

        [Fact]
        public void AnIfDefOnAnUnknownPrefixDropsTheBlockAndReportsIt()
        {
            var result = Document(P("<@ifdef Customer.Name>") + P("kept") + P("<@end>"), new Dictionary<string, object>());

            Assert.Equal(string.Empty, Paragraphs(result));
            Assert.Equal("the model has no member Customer", Assert.Single(result.Findings).Message);
        }

        [Fact]
        public void AnIfDefOnALoopVariableOrCounterKeepsTheBlock()
        {
            var body = P("<@for item Items index=\"n\">") + P("<@ifdef item>") + P("item") + P("<@end>") + P("<@ifdef n>") + P("counter") + P("<@end>") + P("<@end>");

            Assert.Equal("item\ncounter", Text(body, new Dictionary<string, object> { ["Items"] = new[] { "x" } }));
        }

        [Fact]
        public void ALoopRepeatsTheRowsBetweenItsTags()
        {
            var model = new Dictionary<string, object> { ["Lines"] = new object[] { Item("Bolt", 3), Item("Nut", 12) } };
            var table = Table(
                Row(P("Product"), P("Quantity")),
                Row(P("<@for line Lines>")),
                Row(P("<$line.Name>"), P("<$line.Id>")),
                Row(P("<@end>")),
                Row(P("Total"), P("15")));

            var result = Document(table, model);

            var rows = Odt.Content(result.Document).Descendants(Odt.Table + "table-row").ToList();
            Assert.Equal(4, rows.Count);
            Assert.Equal("Product\nQuantity\nBolt\n3\nNut\n12\nTotal\n15", Paragraphs(result));
        }

        [Fact]
        public void ALoopOverNothingRemovesItsBlock()
        {
            var body = P("before") + P("<@for item Items>") + P("<$item>") + P("<@end>") + P("after");

            Assert.Equal("before\nafter", Text(body, new Dictionary<string, object> { ["Items"] = null }));
            Assert.Equal("before\nafter", Text(body, new Dictionary<string, object> { ["Items"] = new object[0] }));

            var unknown = Document(body, new Dictionary<string, object>());
            Assert.Equal("before\nafter", Paragraphs(unknown));
            Assert.Equal("<@for item Items>", Assert.Single(unknown.Findings).Text);
        }

        [Fact]
        public void ALoopOverTextOrAScalarRunsOnce()
        {
            var body = P("<@for item Value>") + P("[", "<$item>", "]") + P("<@end>");

            Assert.Equal("[text]", Text(body, new Dictionary<string, object> { ["Value"] = "text" }));
            Assert.Equal("[42]", Text(body, new Dictionary<string, object> { ["Value"] = 42 }));
        }

        [Fact]
        public void ALoopOverADictionaryVisitsItsKeys()
        {
            var model = new Dictionary<string, object> { ["Map"] = new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 } };

            Assert.Equal("b\na", Text(P("<@for key Map>") + P("<$key>") + P("<@end>"), model));
        }

        [Fact]
        public void ALoopIsOrderedByItsSortKeysAndStable()
        {
            var model = new Dictionary<string, object>
            {
                ["Items"] = new object[] { Item("m", 1, "B"), Item("k", 2, "A"), Item("z", 3, "B"), Item("k", 4, "A") },
            };
            var body = P("<@for item Items sort=\"Group, Name desc\">") + P("<$item.Id>") + P("<@end>");

            Assert.Equal("2\n4\n3\n1", Text(body, model));
        }

        [Fact]
        public void ALoopWhoseKeysCannotBeOrderedKeepsItsOrderAndIsReportedOnce()
        {
            var model = new Dictionary<string, object>
            {
                ["Twice"] = new[] { 1, 2 },
                ["Items"] = new object[] { Item("b", 1), new Dictionary<string, object> { ["Name"] = 7, ["Id"] = 2 }, Item("a", 3) },
            };
            var body = P("<@for t Twice>") + P("<@for item Items sort=\"Name\">") + P("<$item.Id>") + P("<@end>") + P("<@end>");

            var result = Document(body, model);

            Assert.Equal("1\n2\n3\n1\n2\n3", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.CannotSort, finding.Kind);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Equal("Items", finding.Path);
            Assert.Contains("cannot be ordered by sort=\"Name\"", finding.Message);
            Assert.Contains("String", finding.Message);
        }

        [Fact]
        public void ASortKeyTheItemsDoNotHaveIsReportedAndLeavesTheOrder()
        {
            var model = new Dictionary<string, object> { ["Items"] = new object[] { Item("b", 1), Item("a", 2) } };

            var result = Document(P("<@for item Items sort=\"Nmae\">") + P("<$item.Id>") + P("<@end>"), model);

            Assert.Equal("1\n2", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("Nmae", finding.Path);
            Assert.Equal("item has no member Nmae", finding.Message);
        }

        [Fact]
        public void ABlockInsideOneParagraphCostsOnlyTheTags()
        {
            var body = P("Dear ", "<@if Friend>", "friend", "<@end>", "!");

            Assert.Equal("Dear friend!", Text(body, new Dictionary<string, object> { ["Friend"] = true }));
            Assert.Equal("Dear !", Text(body, new Dictionary<string, object> { ["Friend"] = false }));
        }

        [Fact]
        public void TheParagraphsThatHoldBlockTagsGoWithTheirText()
        {
            var body = P("<@if Flag>", " note") + P("kept") + P("also ", "<@end>");

            Assert.Equal("kept", Text(body, new Dictionary<string, object> { ["Flag"] = true }));
        }

        [Fact]
        public void AMalformedTagRendersAsTypedAndIsReported()
        {
            var result = Document(P("<$>") + P("<@for x>"), new Dictionary<string, object>());

            Assert.Equal("<$>\n<@for x>", Paragraphs(result));
            Assert.Equal(2, result.Findings.Count);
            Assert.All(result.Findings, v => Assert.Equal(FindingKind.MalformedTag, v.Kind));
            Assert.Equal("<$> needs a path: <$Invoice.Number>", result.Findings[0].Message);
        }

        [Fact]
        public void TwoBlocksMayEndInOneParagraph()
        {
            var model = new Dictionary<string, object> { ["Flag"] = true, ["Items"] = new object[] { Item("a"), Item("b") } };
            var body = P("<@if Flag>") + P("<@for item Items>") + P("<$item.Name>") + P("<@end>", " ", "<@end>");

            // The paragraph that holds both end tags is the end paragraph of both blocks and leaves the document once
            Assert.Equal("a\nb", Text(body, model));
        }

        [Fact]
        public void TwoBlocksMayBeginInOneParagraph()
        {
            var model = new Dictionary<string, object> { ["Flag"] = true, ["Items"] = new object[] { Item("a"), Item("b") } };
            var body = P("<@if Flag>", " ", "<@for item Items>") + P("<$item.Name>") + P("<@end>") + P("<@end>");

            // The paragraph that holds both begin tags is the begin paragraph of both blocks and leaves the document once
            Assert.Equal("a\nb", Text(body, model));
        }

        [Fact]
        public void ABlockThatWouldCutATableThrowsWhenTheTemplateRenders()
        {
            var exception = Assert.Throws<TemplateException>(() => Template(Table(Row(P("<@if Flag>"), P("<@end>")))).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Equal("<@if Flag>", finding.Text);
            Assert.Contains("two cells of one row", finding.Message);
            Assert.Contains("two cells of one row", exception.Message);
        }

        [Fact]
        public void ABlockThatEndsOutsideItsTableThrowsWhenTheTemplateRenders()
        {
            var exception = Assert.Throws<TemplateException>(() => Template(Table(Row(P("<@for line Lines>"))) + P("<@end>")).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Contains("ends outside the table", exception.Message);
        }

        [Theory]
        [InlineData("<@for line Lines>", "this block never ends")]
        [InlineData("<@end>", "closes nothing")]
        public void AnUnbalancedBlockThrowsWhenTheTemplateRenders(string tag, string message)
        {
            var exception = Assert.Throws<TemplateException>(() => Template(P("x") + P(tag)).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.UnbalancedBlock, finding.Kind);
            Assert.Equal(tag, finding.Text);
            Assert.Contains(message, finding.Message);
            Assert.Equal(2, finding.Location.Paragraph);
            Assert.Contains(tag, exception.Message);
        }

        [Fact]
        public void AnUnbalancedBlockInAHeaderThrowsWhenTheTemplateRenders()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P("<@if Flag>") + "</style:header></style:master-page></office:master-styles>";

            var exception = Assert.Throws<TemplateException>(() => Template(P("x"), styles: header).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.UnbalancedBlock, finding.Kind);
            Assert.Equal("styles.xml", finding.Location.Part);
        }
    }
}
