// <copyright file="ElseTests.cs" company="Allors bvba">
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

    /// <summary>The branches of a condition: else, else if, else ifdef and not; where an else may sit; what cannot render.</summary>
    public class ElseTests
    {
        [Fact]
        public void AnElseTakesOverWhenTheConditionFails()
        {
            var body = P("<@if Flag>") + P("yes") + P("<@else>") + P("no") + P("<@end>") + P("after");

            Assert.Equal("yes\nafter", Text(body, new Dictionary<string, object> { ["Flag"] = true }));
            Assert.Equal("no\nafter", Text(body, new Dictionary<string, object> { ["Flag"] = false }));
            Assert.Equal("no\nafter", Text(body, new Dictionary<string, object> { ["Flag"] = null }));

            var unknown = Document(body, new Dictionary<string, object>());
            Assert.Equal("no\nafter", Paragraphs(unknown));
            Assert.Equal("<@if Flag>", Assert.Single(unknown.Findings).Text);
        }

        [Fact]
        public void AnElseInsideOneParagraphCostsOnlyTheTags()
        {
            var body = P("Dear ", "<@if Friend>", "friend", "<@else>", "customer", "<@end>", "!");

            Assert.Equal("Dear friend!", Text(body, new Dictionary<string, object> { ["Friend"] = true }));
            Assert.Equal("Dear customer!", Text(body, new Dictionary<string, object> { ["Friend"] = false }));
        }

        [Fact]
        public void AChainTakesTheFirstBranchWhoseTestHolds()
        {
            var body = P("<@if A>") + P("a") + P("<@else if B>") + P("b") + P("<@else ifdef C>") + P("c") + P("<@else>") + P("none") + P("<@end>");

            Assert.Equal("a", Text(body, new Dictionary<string, object> { ["A"] = true, ["B"] = true, ["C"] = null }));
            Assert.Equal("b", Text(body, new Dictionary<string, object> { ["A"] = false, ["B"] = "x", ["C"] = null }));
            Assert.Equal("c", Text(body, new Dictionary<string, object> { ["A"] = false, ["B"] = 0, ["C"] = null }));
            Assert.Equal("none", Text(body, new Dictionary<string, object> { ["A"] = string.Empty, ["B"] = null }));
        }

        [Fact]
        public void AChainWithoutAFinalElseWritesNothingWhenNoTestHolds()
        {
            var body = P("before") + P("<@if A>") + P("a") + P("<@else if B>") + P("b") + P("<@end>") + P("after");

            Assert.Equal("before\nafter", Text(body, new Dictionary<string, object> { ["A"] = false, ["B"] = false }));
        }

        [Fact]
        public void AnElseIfOnAnUnknownMemberIsReportedOnTheElseTag()
        {
            var result = Document(P("<@if A>") + P("a") + P("<@else if Nope>") + P("b") + P("<@else>") + P("c") + P("<@end>"), new Dictionary<string, object> { ["A"] = false });

            Assert.Equal("c", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("<@else if Nope>", finding.Text);
            Assert.Equal(3, finding.Location.Paragraph);
        }

        [Fact]
        public void NotNegatesATest()
        {
            var ifNot = P("<@if not Flag>") + P("off") + P("<@else>") + P("on") + P("<@end>");
            Assert.Equal("off", Text(ifNot, new Dictionary<string, object> { ["Flag"] = false }));
            Assert.Equal("off", Text(ifNot, new Dictionary<string, object> { ["Flag"] = string.Empty }));
            Assert.Equal("on", Text(ifNot, new Dictionary<string, object> { ["Flag"] = 1 }));

            var ifdefNot = P("<@ifdef not Customer.Name>") + P("nameless") + P("<@else ifdef not Customer.Email>") + P("no email") + P("<@else>") + P("both") + P("<@end>");
            Assert.Equal("nameless", Text(ifdefNot, new Dictionary<string, object> { ["Customer"] = new Dictionary<string, object> { ["Email"] = "e" } }));
            Assert.Equal("no email", Text(ifdefNot, new Dictionary<string, object> { ["Customer"] = new Dictionary<string, object> { ["Name"] = null } }));
            Assert.Equal("both", Text(ifdefNot, new Dictionary<string, object> { ["Customer"] = new Dictionary<string, object> { ["Name"] = "n", ["Email"] = "e" } }));
            Assert.Equal("nameless", Text(ifdefNot, new Dictionary<string, object> { ["Customer"] = null }));
        }

        [Fact]
        public void AnElseOnRowLevelTagsRemovesOnlyTheRowsOfTheTags()
        {
            var table = Table(Row(P("Header")), Row(P("<@if Flag>")), Row(P("yes")), Row(P("<@else>")), Row(P("no")), Row(P("<@end>")));

            var result = Document(table, new Dictionary<string, object> { ["Flag"] = false });

            Assert.Equal(2, Odt.Content(result.Document).Descendants(Odt.Table + "table-row").Count());
            Assert.Equal("Header\nno", Paragraphs(result));
        }

        [Fact]
        public void AnElseInsideALoopBelongsToTheConditionNotTheLoop()
        {
            var model = new Dictionary<string, object> { ["Items"] = new object[] { Item("a", 1), Item("b", 0) } };
            var body = P("<@for item Items>") + P("<@if item.Id>") + P("<$item.Name>", " on") + P("<@else>") + P("<$item.Name>", " off") + P("<@end>") + P("<@end>");

            Assert.Equal("a on\nb off", Text(body, model));
        }

        [Fact]
        public void NestedConditionsKeepTheirOwnElses()
        {
            var body = P("<@if A>") + P("<@if B>") + P("ab") + P("<@else>") + P("a") + P("<@end>") + P("<@else>") + P("none") + P("<@end>");

            Assert.Equal("ab", Text(body, new Dictionary<string, object> { ["A"] = true, ["B"] = true }));
            Assert.Equal("a", Text(body, new Dictionary<string, object> { ["A"] = true, ["B"] = false }));
            Assert.Equal("none", Text(body, new Dictionary<string, object> { ["A"] = false, ["B"] = true }));
        }

        [Fact]
        public void TheParagraphThatHoldsAnElseGoesWithItsText()
        {
            var body = P("<@if A>") + P("x") + P("also ", "<@else>") + P("y") + P("<@end>");

            Assert.Equal("x", Text(body, new Dictionary<string, object> { ["A"] = true }));
            Assert.Equal("y", Text(body, new Dictionary<string, object> { ["A"] = false }));
        }

        [Fact]
        public void AnElseInAHeaderRendersAgainstTheModel()
        {
            var header = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P("<@if Paid>", "paid", "<@else>", "due", "<@end>") + "</style:header></style:master-page></office:master-styles>";

            var result = Document(P("body"), new Dictionary<string, object> { ["Paid"] = false }, styles: header);

            Assert.Equal("due", Odt.Paragraphs(Odt.Styles(result.Document)));
        }

        [Theory]
        [InlineData("<@else>", "belongs to no condition")]
        [InlineData("<@for i Items><@else><@end>", "sits in a loop")]
        [InlineData("<@if A><@else><@else><@end>", "already has an <@else>")]
        [InlineData("<@if A><@else><@else if B><@end>", "comes after the <@else>")]
        public void AnElseThatBelongsToNoConditionThrowsWhenTheTemplateRenders(string tags, string message)
        {
            var body = string.Concat(tags.Split('>').Where(v => v.Length > 0).Select(v => P(v + ">")));

            var exception = Assert.Throws<TemplateException>(() => Template(body).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.UnbalancedBlock, finding.Kind);
            Assert.StartsWith("<@else", finding.Text);
            Assert.Contains(message, finding.Message);
        }

        [Fact]
        public void AnElseInTheParagraphOfTheBeginTagThrowsWhenTheTemplateRenders()
        {
            var exception = Assert.Throws<TemplateException>(() => Template(P("<@if A>", "<@else>") + P("x") + P("<@end>")).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Equal("<@if A>", finding.Text);
            Assert.Contains("the else sits in the paragraph that holds the block's begin tag", finding.Message);
            Assert.Contains("give the else a paragraph of its own", finding.Message);
        }

        [Fact]
        public void AnElseInTheRowOfTheEndTagThrowsWhenTheTemplateRenders()
        {
            var exception = Assert.Throws<TemplateException>(() => Template(Table(Row(P("<@if A>")), Row(P("x")), Row(P("<@else>") + P("<@end>")))).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Contains("the else sits in the row that holds the block's <@end>", finding.Message);
        }

        [Fact]
        public void AnElseInATableWhileTheBlockIsOutsideThrowsWhenTheTemplateRenders()
        {
            var exception = Assert.Throws<TemplateException>(() => Template(P("<@if A>") + Table(Row(P("<@else>"))) + P("<@end>")).Render(new Dictionary<string, object>()));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Contains("would remove the table", finding.Message);
        }
    }
}
