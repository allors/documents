// <copyright file="ValidationTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Allors.Documents;
    using Allors.Documents.Reflection;
    using Xunit;

    public class ValidationTests
    {
        private static readonly IShape Root = ReflectionShape.Arguments<Model>();

        [Fact]
        public void ACleanTemplateHasNoFindings()
        {
            var body =
                P("<$Invoice.Total format=\"0000\" culture=\"nl-BE\">") +
                P("<$Invoice.Date format=\"date\">") +
                P("<@if Invoice.BillTo>") + P("<$Invoice.Paid true=\"Ja\" false=\"Nee\">") + P("<@end>") +
                P("<@ifdef Invoice.BillTo.Name>") + P("<$Invoice.BillTo.Name>") + P("<@end>") +
                P("<@for line Invoice.Lines sort=\"Product, Quantity desc\">") + Tags("<$line.Product>", "<$line.Quantity format=\"number\">") + P("<@end>") +
                P(Odt.Frame("Logo", "Pictures/logo.png", "<$Logo empty=\"keep\">", "The logo"));
            var settings = new Dictionary<string, string> { ["culture"] = "nl-BE", ["number-culture"] = "en-GB", ["date-culture"] = "invariant", ["date"] = "dd-MMM-yyyy", ["datetime"] = "yyyy-MM-dd HH:mm", ["number"] = "N2", ["true"] = "Ja" };

            var validation = Validate(body, settings);

            Assert.Empty(validation.Findings);
            Assert.True(validation.IsValid);
            Assert.NotNull(validation.Inspection);
        }

        [Fact]
        public void AnUnknownMemberIsAnErrorThatNamesTheShapeAndTheSegment()
        {
            var validation = Validate(P("<$Invoice.Nubmer>") + P("<$Invoce.Number>") + P("<$Invoice.BillTo.Street>"));

            Assert.Equal(3, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.UnknownMember, v.Kind));
            Assert.All(validation.Findings, v => Assert.Equal(FindingSeverity.Error, v.Severity));
            Assert.Equal("Invoice has no member Nubmer", validation.Findings[0].Message);
            Assert.Equal("Invoice.Nubmer", validation.Findings[0].Path);
            Assert.Equal("<$Invoice.Nubmer>", validation.Findings[0].Text);
            Assert.Equal(1, validation.Findings[0].Location.Paragraph);
            Assert.Equal("Model has no member Invoce", validation.Findings[1].Message);
            Assert.Equal("Party has no member Street", validation.Findings[2].Message);
            Assert.False(validation.IsValid);
        }

        [Fact]
        public void TheRootIsExactCaseAndNestedMembersFollowTheFirstLetterRule()
        {
            var validation = Validate(P("<$invoice.Number>") + P("<$Invoice.number>"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal("Model has no member invoice", finding.Message);
        }

        [Fact]
        public void LoopVariablesAreInScopeInsideTheLoopOnly()
        {
            var body =
                P("<@for line Invoice.Lines>") + P("<$line.Product>") + P("<$line.Prodcut>") + P("<@end>") +
                P("<$line.Product>");

            var validation = Validate(body);

            Assert.Equal(2, validation.Findings.Count);
            Assert.Equal("Line has no member Prodcut", validation.Findings[0].Message);
            Assert.Equal("Model has no member line", validation.Findings[1].Message);
        }

        [Fact]
        public void NestedLoopsSeeOuterVariablesAndShadowThem()
        {
            var body =
                P("<@for line Invoice.Lines>") +
                P("<@for tag line.Tags>") + Tags("<$line.Product>", "<$tag.Length>", "<$Invoice.Number>") + P("<@end>") +
                P("<@for line line.Tags>") + P("<$line.Length>") + P("<$line.Product>") + P("<@end>") +
                P("<@end>");

            var validation = Validate(body);

            var finding = Assert.Single(validation.Findings);
            Assert.Equal("String has no member Product", finding.Message);
        }

        [Fact]
        public void ALoopOverANonCollectionIsAWarningAndLoopsOverTheValue()
        {
            var validation = Validate(P("<@for n Invoice.Number>") + P("<$n.Length>") + P("<@end>"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.NotACollection, finding.Kind);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Contains("'Invoice.Number' is String, not a collection", finding.Message);
            Assert.True(validation.IsValid);
        }

        [Fact]
        public void AnUnknownLoopPathMakesTheVariableOpen()
        {
            var validation = Validate(P("<@for x Invoice.Nothing>") + P("<$x.Anything.AtAll>") + P("<@end>"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal("Invoice has no member Nothing", finding.Message);
        }

        [Fact]
        public void SortKeysAreCheckedAgainstTheItem()
        {
            var validation = Validate(P("<@for line Invoice.Lines sort=\"Product.Length, Prodcut desc\">") + P("<@end>"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("Prodcut", finding.Path);
            Assert.Equal("sort key 'Prodcut': Line has no member Prodcut", finding.Message);
        }

        [Fact]
        public void UnknownAttributesAreErrors()
        {
            var validation = Validate(P("<$Invoice.Number colour=\"red\">") + P("<@for line Invoice.Lines where=\"x\">") + P("<@end>"));

            Assert.Equal(2, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.UnknownAttribute, v.Kind));
            Assert.Equal("a binding has no attribute 'colour': use format, culture, true, false, map, blank", validation.Findings[0].Message);
            Assert.Equal("a loop has no attribute 'where': use sort, index, separator", validation.Findings[1].Message);
        }

        [Fact]
        public void ACultureThatIsNoCultureIsAnError()
        {
            var validation = Validate(P("<$Invoice.Number culture=\"xx-NOPE\">"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.BadCulture, finding.Kind);
            Assert.Equal("'xx-NOPE' is not a culture: use a name like nl-BE or en-GB, or invariant", finding.Message);
        }

        [Fact]
        public void ACultureOfAKindIsNoTagAttribute()
        {
            var validation = Validate(P("<$Invoice.Number number-culture=\"en-GB\">"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.UnknownAttribute, finding.Kind);
            Assert.Equal("a binding has no attribute 'number-culture': use format, culture, true, false, map, blank", finding.Message);
        }

        [Fact]
        public void AFormatThatNeitherADateNorANumberAcceptsIsAnError()
        {
            var validation = Validate(P("<$Invoice.Number format=\"Q\">") + P("<$Invoice.Number format=\"yyyy\">") + P("<$Invoice.Number format=\"N2\">") + P("<$Invoice.Number format=\"DateTime\">"), root: DictionaryShape.Open);

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.BadPattern, finding.Kind);
            Assert.Equal("<$Invoice.Number format=\"Q\">", finding.Text);
        }

        [Fact]
        public void AFormatOfTheWrongFamilyForTheKindIsAnError()
        {
            var body = P("<$Invoice.Date format=\"N2\">") + P("<$Invoice.Date format=\"number\">") + P("<$Invoice.Total format=\"yyyy\">") + P("<$Invoice.Total format=\"date\">") + P("<$Invoice.Date format=\"dd-MMM-yyyy\">") + P("<$Invoice.Date format=\"datetime\">") + P("<$Invoice.Total format=\"#,##0.00\">") + P("<$Invoice.Total format=\"number\">");

            var validation = Validate(body);

            Assert.Equal(4, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.BadPattern, v.Kind));
            Assert.All(validation.Findings, v => Assert.Equal(FindingSeverity.Error, v.Severity));
            Assert.Equal("'N2' is not a date pattern, and 'Invoice.Date' is a date and time: use the letters d, M, y, h, H, m, s, f, F, t, z, K and g with separators and quoted text, such as dd-MMM-yyyy, or a standard letter such as d, D or g", validation.Findings[0].Message);
            Assert.Equal("'number' names the document's number pattern, and 'Invoice.Date' is a date and time: use date, time or datetime, or a date pattern", validation.Findings[1].Message);
            Assert.Equal("'yyyy' is not a number pattern, and 'Invoice.Total' is a number: use a standard letter with its precision such as N2, F0 or C, or a pattern with 0 and # such as #,##0.00", validation.Findings[2].Message);
            Assert.Equal("'date' names the document's date pattern, and 'Invoice.Total' is a number: use number, or a number pattern", validation.Findings[3].Message);
        }

        /// <summary>.NET prints "yyyy-MM-dd Q" and "EUR" without complaint; the grammar says what the author meant them to be.</summary>
        [Fact]
        public void APatternIsCheckedByItsGrammarNotByTrial()
        {
            var body = P("<$Invoice.Date format=\"yyyy-MM-dd Q\">") + P("<$Invoice.Total format=\"EUR\">") + P("<$Invoice.Date format=\"dd 'at' HH:mm\">") + P("<$Invoice.Total format=\"#,##0.00 'EUR'\">") + P("<$Invoice.Date format=\"HH\\\\hmm\">");

            var validation = Validate(body);

            Assert.Equal(2, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.BadPattern, v.Kind));
            Assert.Equal("<$Invoice.Date format=\"yyyy-MM-dd Q\">", validation.Findings[0].Text);
            Assert.Equal("<$Invoice.Total format=\"EUR\">", validation.Findings[1].Text);
        }

        [Fact]
        public void AFormatOrBooleanWordsOnAKindThatPrintsWithoutThemIsAWarning()
        {
            var body = P("<$Invoice.Number format=\"N2\">") + P("<$Invoice.Paid format=\"N2\">") + P("<$Invoice.BillTo format=\"N2\">") + P("<$Logo format=\"N2\">") + P("<$Invoice.Number true=\"Ja\">") + P("<$Invoice.Total false=\"Nee\">") + P("<$Invoice.Paid true=\"Ja\" false=\"Nee\">") + P("<$Invoice.Total format=\"N2\">");

            var validation = Validate(body);

            Assert.Equal(6, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.FormatIgnored, v.Kind));
            Assert.All(validation.Findings, v => Assert.Equal(FindingSeverity.Warning, v.Severity));
            Assert.True(validation.IsValid);
            Assert.Equal("the format is ignored: 'Invoice.Number' is text and prints without a pattern", validation.Findings[0].Message);
            Assert.Equal("the format is ignored: 'Invoice.Paid' is a boolean and prints without a pattern", validation.Findings[1].Message);
            Assert.Equal("the format is ignored: 'Invoice.BillTo' is an object and prints without a pattern", validation.Findings[2].Message);
            Assert.Equal("the format is ignored: 'Logo' is a picture and prints without a pattern", validation.Findings[3].Message);
            Assert.Equal("the true and false words are ignored: 'Invoice.Number' is text, not a boolean", validation.Findings[4].Message);
            Assert.Equal("the true and false words are ignored: 'Invoice.Total' is a number, not a boolean", validation.Findings[5].Message);
        }

        /// <summary>A collection bound as a value prints its items, so the format is checked against them.</summary>
        [Fact]
        public void AFormatOnACollectionIsCheckedAgainstItsItems()
        {
            var body = P("<@for line Invoice.Lines>") + Tags("<$line.Tags format=\"N2\">", "<$line.Tags>") + P("<@end>") + P("<$Invoice.Lines format=\"N2\">") + P("<$Parties format=\"yyyy\">");

            var validation = Validate(body);

            Assert.Equal(3, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.FormatIgnored, v.Kind));
            Assert.Equal("the format is ignored: 'line.Tags' is text and prints without a pattern", validation.Findings[0].Message);
            Assert.Equal("the format is ignored: 'Invoice.Lines' is an object and prints without a pattern", validation.Findings[1].Message);
            Assert.Equal("the format is ignored: 'Parties' is an object and prints without a pattern", validation.Findings[2].Message);
        }

        /// <summary>An open value and a value of another kind, such as a Guid, print with whatever pattern they are given.</summary>
        [Fact]
        public void AnOpenOrOtherValueTakesAnyPattern()
        {
            var root = new DictionaryShape(new Dictionary<string, IShape> { ["Id"] = ReflectionShape.Of<Guid>(), ["Anything"] = DictionaryShape.Open });

            var validation = Validate(P("<$Id format=\"N\">") + P("<$Anything format=\"yyyy\">") + P("<$Anything true=\"Ja\">") + P("<$Id format=\"Q\">"), root: root);

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.BadPattern, finding.Kind);
            Assert.Equal("<$Id format=\"Q\">", finding.Text);
        }

        [Fact]
        public void ASortKeyThatDoesNotOrderIsAnError()
        {
            var validation = Validate(P("<@for line Invoice.Lines sort=\"Tags, Picture desc, Product, Quantity\">") + P("<@end>") + P("<@for party Parties sort=\"Name\">") + P("<@end>"));

            Assert.Equal(2, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.CannotSort, v.Kind));
            Assert.All(validation.Findings, v => Assert.Equal(FindingSeverity.Error, v.Severity));
            Assert.Equal("Tags", validation.Findings[0].Path);
            Assert.Equal("sort key 'Tags' is a collection (String[]), which does not order: sort by a text, number, boolean, date or time member of it, such as Tags.Name", validation.Findings[0].Message);
            Assert.Equal("sort key 'Picture' is a picture, which does not order: sort by a text, number, boolean, date or time member of it, such as Picture.Name", validation.Findings[1].Message);
        }

        [Fact]
        public void ASortKeyOnAnObjectIsAnError()
        {
            var validation = Validate(P("<@for party Parties sort=\"BillTo\">") + P("<@end>"), root: new DictionaryShape(new Dictionary<string, IShape> { ["Parties"] = ReflectionShape.Of<Invoice[]>() }));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.CannotSort, finding.Kind);
            Assert.Equal("sort key 'BillTo' is an object (Party), which does not order: sort by a text, number, boolean, date or time member of it, such as BillTo.Name", finding.Message);
        }

        [Fact]
        public void APictureTagOnAValueThatIsNoPictureIsAnError()
        {
            var body = P(Odt.Frame("A", "Pictures/a.png", "<$Invoice.Number>")) + P(Odt.Frame("B", "Pictures/b.png", "<$Invoice>")) + P(Odt.Frame("C", "Pictures/c.png", "<$Invoice.Lines>")) + P(Odt.Frame("D", "Pictures/d.png", "<$Logo>"));

            var validation = Validate(body);

            Assert.Equal(3, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.NotAPicture, v.Kind));
            Assert.All(validation.Findings, v => Assert.Equal(FindingSeverity.Error, v.Severity));
            Assert.Equal("'Invoice.Number' is text, not a picture: bind the frame to a member that holds the bytes of a picture", validation.Findings[0].Message);
            Assert.Equal("'Invoice' is an object (Invoice), not a picture: bind the frame to a member that holds the bytes of a picture", validation.Findings[1].Message);
            Assert.Equal("'Invoice.Lines' is a collection (Line[]), not a picture: bind the frame to a member that holds the bytes of a picture", validation.Findings[2].Message);
            Assert.Equal("Invoice.Number", validation.Findings[0].Path);
        }

        [Fact]
        public void APictureTagOnAnOpenValueIsNotChecked() => Assert.Empty(Validate(P(Odt.Frame("A", "Pictures/a.png", "<$Anything.AtAll>")), root: DictionaryShape.Open).Findings);

        [Fact]
        public void DocumentPatternsFollowTheGrammar()
        {
            var settings = new Dictionary<string, string> { ["date"] = "yyyy-MM-dd Q", ["time"] = "HH'h'mm", ["datetime"] = "N2", ["number"] = "EUR" };

            var validation = Validate(P("<$Invoice.Number>"), settings);

            Assert.Equal(3, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.BadPattern, v.Kind));
            Assert.Equal("'yyyy-MM-dd Q' is not a date format", validation.Findings[0].Message);
            Assert.Equal("'N2' is not a date format", validation.Findings[1].Message);
            Assert.Equal("'EUR' is not a number format", validation.Findings[2].Message);
        }

        [Fact]
        public void TheLoopCounterIsInScopeInsideTheLoopAndHasNoMembers()
        {
            var validation = Validate(P("<@for line Invoice.Lines index=\"n\">") + Tags("<$n>", "<$n.Value>", "<$line.Product>") + P("<@end>") + P("<$n>"));

            Assert.Equal(2, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.UnknownMember, v.Kind));
            Assert.Equal("the loop counter has no member Value", validation.Findings[0].Message);
            Assert.Equal("Model has no member n", validation.Findings[1].Message);
        }

        [Fact]
        public void AMemberOfSomeVariantsIsAnErrorOutsideAGuard()
        {
            var validation = Validate(P("<$Invoice.BillTo.FirstName>"), root: PolymorphicRoot());

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("FirstName is a member of Person but not of every Party: put the block in <@ifdef Invoice.BillTo.FirstName> or <@if Invoice.BillTo.FirstName>", finding.Message);
        }

        [Theory]
        [InlineData("<@ifdef Invoice.BillTo.FirstName>")]
        [InlineData("<@if Invoice.BillTo.FirstName>")]
        public void AGuardNarrowsThePrefixInsideItsBlock(string guard)
        {
            var body =
                P(guard) + Tags("<$Invoice.BillTo.FirstName>", "<$Invoice.BillTo.LastName>", "<$Invoice.BillTo.Name>") + P("<$Invoice.BillTo.Nonsense>") + P("<@end>") +
                P("<$Invoice.BillTo.FirstName>");

            var validation = Validate(body, root: PolymorphicRoot());

            Assert.Equal(2, validation.Findings.Count);
            Assert.Equal("Person has no member Nonsense", validation.Findings[0].Message);
            Assert.StartsWith("FirstName is a member of Person but not of every Party", validation.Findings[1].Message);
        }

        [Fact]
        public void AGuardOnAMemberNoVariantHasIsAnError()
        {
            var validation = Validate(P("<@ifdef Invoice.BillTo.Nonsense>") + P("<$Invoice.BillTo.Nonsense>") + P("<@end>"), root: PolymorphicRoot());

            Assert.Equal(2, validation.Findings.Count);
            Assert.Equal("Party has no member Nonsense", validation.Findings[0].Message);
            Assert.Equal("<@ifdef Invoice.BillTo.Nonsense>", validation.Findings[0].Text);
        }

        [Fact]
        public void AGuardNarrowsALoopVariableToo()
        {
            var body =
                P("<@for p Parties>") +
                P("<@ifdef p.FirstName>") + P("<$p.FirstName>") + P("<@end>") +
                P("<$p.FirstName>") +
                P("<@end>");

            var validation = Validate(body, root: PolymorphicRoot());

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(5, finding.Location.Paragraph);
            Assert.Contains("<@ifdef p.FirstName>", finding.Message);
        }

        [Fact]
        public void AGuardOnACollectionMemberLetsALoopUseIt()
        {
            var body = P("<@ifdef Invoice.BillTo.Children>") + P("<@for c Invoice.BillTo.Children>") + P("<$c.FirstName>") + P("<@end>") + P("<@end>");

            var validation = Validate(body, root: PolymorphicRoot());

            Assert.Empty(validation.Findings);
        }

        [Fact]
        public void BlocksInRowsOfOneTableOrParagraphsOfOneContainerAreFine()
        {
            var body =
                Table(Row(Cell(P("<@for line Invoice.Lines>"))) + Row(Cell(P("<$line.Product>")) + Cell(P("<$line.Quantity>"))) + Row(Cell(P("<@end>")))) +
                Table(Row(Cell(P("<@if Invoice.BillTo>") + P("<$Invoice.BillTo.Name>") + P("<@end>"))));

            Assert.Empty(Validate(body).Findings);
        }

        [Fact]
        public void ABlockThatBeginsInARowAndEndsOutsideTheTableIsAnError()
        {
            var body = Table(Row(Cell(P("<@for line Invoice.Lines>"))) + Row(Cell(P("<$line.Product>")))) + P("<@end>");

            var finding = Assert.Single(Validate(body).Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Contains("begins in a table row and ends outside the table", finding.Message);
        }

        [Fact]
        public void ABlockInTwoCellsOfOneRowIsAnError()
        {
            var body = Table(Row(Cell(P("<@if Invoice.BillTo>")) + Cell(P("<$Invoice.BillTo.Name>")) + Cell(P("<@end>"))));

            var finding = Assert.Single(Validate(body).Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Contains("the tags sit in two cells of one row", finding.Message);
        }

        [Fact]
        public void ABlockThatBeginsOutsideATableAndEndsInsideIsAnError()
        {
            var body = P("<@if Invoice.BillTo>") + Table(Row(Cell(P("<@end>"))));

            var finding = Assert.Single(Validate(body).Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Contains("ends in a table row", finding.Message);
        }

        [Fact]
        public void OtherTextInABlockTagsParagraphIsAWarning()
        {
            var body = "<text:p>Total: " + Odt.Placeholder("<@if Invoice.BillTo>") + "</text:p>" + P("<$Invoice.BillTo.Name>") + "<text:p>" + Odt.Placeholder("<@end>") + Odt.Placeholder("<$Invoice.Number>") + "</text:p>";

            var validation = Validate(body);

            Assert.Equal(2, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.MixedText, v.Kind));
            Assert.All(validation.Findings, v => Assert.Equal(FindingSeverity.Warning, v.Severity));
            Assert.Contains("'Total:' with it: give the tag its own paragraph", validation.Findings[0].Message);
            Assert.Contains("'<$Invoice.Number>' with it", validation.Findings[1].Message);
            Assert.True(validation.IsValid);
        }

        [Fact]
        public void OtherContentInABlockTagsRowIsAWarning()
        {
            var body = Table(Row(Cell(P("<@for line Invoice.Lines>")) + Cell(P("<$line.Product>"))) + Row(Cell(P("<@end>"))));

            var finding = Assert.Single(Validate(body).Findings);
            Assert.Equal(FindingKind.MixedText, finding.Kind);
            Assert.Contains("the row that holds this tag is removed with the tag, and '<$line.Product>' with it", finding.Message);
        }

        [Fact]
        public void ABlockInsideOneParagraphCostsOnlyTheTags()
        {
            var body =
                "<text:p>" + Odt.Placeholder("<@if Invoice.BillTo>") + Odt.Placeholder("<$Invoice.BillTo.Name>") + Odt.Placeholder("<@end>") + " - " + Odt.Placeholder("<$Invoice.Number>") + "</text:p>" +
                "<text:p>Orders: " + Odt.Placeholder("<@for line Invoice.Lines>") + Odt.Placeholder("<$line.Product>") + ", " + Odt.Placeholder("<@end>") + "</text:p>";

            Assert.Empty(Validate(body).Findings);
        }

        [Fact]
        public void AnEndInsideAListOfTheSameCellIsFine()
        {
            var body = Table(Row(Cell(P("<@if Invoice.BillTo>") + P("<$Invoice.BillTo.Name>") + "<text:list><text:list-header>" + P("<@end>") + "</text:list-header></text:list>")));

            Assert.Empty(Validate(body).Findings);
        }

        [Fact]
        public void OtherTextInAListThatHoldsABlockTagIsAWarning()
        {
            var body = P("<@if Invoice.BillTo>") + P("<$Invoice.BillTo.Name>") + "<text:list><text:list-item>" + P("<@end>") + P("Thank you") + "</text:list-item></text:list>";

            var finding = Assert.Single(Validate(body).Findings);
            Assert.Equal(FindingKind.MixedText, finding.Kind);
            Assert.Contains("the list that holds this tag is removed with the tag, and 'Thank you' with it: give the tag its own list", finding.Message);
        }

        [Fact]
        public void TwoBlockTagsInOneParagraphAreNotMixedText()
        {
            var body = P("<@if Invoice.BillTo>") + P("<@for line Invoice.Lines>") + P("<$line.Product>") + "<text:p>" + Odt.Placeholder("<@end>") + " " + Odt.Placeholder("<@end>") + "</text:p>";

            Assert.Empty(Validate(body).Findings);
        }

        [Fact]
        public void DocumentSettingsAreChecked()
        {
            var settings = new Dictionary<string, string> { ["colour"] = "red", ["culture"] = "xx-NOPE", ["date"] = "Q", ["number"] = "N2", ["time"] = string.Empty, ["date-culture"] = "xx-NOPE", ["number-culture"] = "invariant" };

            var validation = Validate(P("<$Invoice.Number>"), settings);

            Assert.Equal(4, validation.Findings.Count);
            Assert.Equal(FindingKind.UnknownSetting, validation.Findings[0].Kind);
            Assert.Equal(FindingSeverity.Warning, validation.Findings[0].Severity);
            Assert.Equal("colour", validation.Findings[0].Text);
            Assert.Equal("meta.xml", validation.Findings[0].Location.Part);
            Assert.Equal(FindingKind.BadCulture, validation.Findings[1].Kind);
            Assert.Equal("culture", validation.Findings[1].Text);
            Assert.Equal(FindingKind.BadCulture, validation.Findings[2].Kind);
            Assert.Equal("date-culture", validation.Findings[2].Text);
            Assert.Equal(FindingKind.BadPattern, validation.Findings[3].Kind);
            Assert.Equal("'Q' is not a date format", validation.Findings[3].Message);
        }

        [Fact]
        public void PicturesAreCheckedInTheirScope()
        {
            var body = P(Odt.Frame("Logo", "Pictures/a.png", "<$Lgoo>")) + P("<@for line Invoice.Lines>") + P(Odt.Frame("Picture", "Pictures/b.png", "<$line.Picture>") + Odt.Frame("Picture", "Pictures/c.png", "<$line.Pitcure>")) + P("<@end>");

            var validation = Validate(body);

            Assert.Equal(2, validation.Findings.Count);
            Assert.Equal("Model has no member Lgoo", validation.Findings[0].Message);
            Assert.Equal("<$Lgoo>", validation.Findings[0].Text);
            Assert.Equal(FindingKind.UnknownMember, validation.Findings[0].Kind);
            Assert.Equal("Line has no member Pitcure", validation.Findings[1].Message);
            Assert.Equal("<$line.Pitcure>", validation.Findings[1].Text);
        }

        [Fact]
        public void PictureAttributesAndTheirValuesAreChecked()
        {
            var body = P(Odt.Frame("Logo", "Pictures/a.png", "<$Logo size=\"contain\">")) + P(Odt.Frame("Logo", "Pictures/a.png", "<$Logo empty=\"maybe\">")) +
                       P(Odt.Frame("Logo", "Pictures/a.png", "<$Logo empty=\"KEEP\" FIT=\"Contain\">")) + P(Odt.Frame("Logo", "Pictures/a.png", "<$Logo fit=\"cover\">"));

            var validation = Validate(body);

            Assert.Equal(3, validation.Findings.Count);
            Assert.Equal(FindingKind.UnknownAttribute, validation.Findings[0].Kind);
            Assert.Equal("a picture has no attribute 'size': use empty, fit", validation.Findings[0].Message);
            Assert.Equal("<$Logo size=\"contain\">", validation.Findings[0].Text);
            Assert.Equal(FindingKind.BadValue, validation.Findings[1].Kind);
            Assert.Equal("empty=\"maybe\": use remove or keep", validation.Findings[1].Message);
            Assert.Equal(2, validation.Findings[1].Location.Paragraph);
            Assert.Equal(FindingKind.BadValue, validation.Findings[2].Kind);
            Assert.Equal("fit=\"cover\": use fill, contain, width or height", validation.Findings[2].Message);
            Assert.Equal("<$Logo fit=\"cover\">", validation.Findings[2].Text);
            Assert.Equal(4, validation.Findings[2].Location.Paragraph);
        }

        [Fact]
        public void FitIsNoAttributeOfATextBinding()
        {
            var validation = Validate(P("<$Logo fit=\"contain\">"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.UnknownAttribute, finding.Kind);
            Assert.Equal("a binding has no attribute 'fit': use format, culture, true, false, map, blank", finding.Message);
        }

        [Fact]
        public void ADescriptionThatIsNotOnePictureTagIsAMalformedTag()
        {
            var body = P(Odt.Frame("Logo", "Pictures/a.png", "Logo: <$Logo>"));

            var validation = Validate(body);

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.MalformedTag, finding.Kind);
            Assert.Equal("Logo: <$Logo>", finding.Text);
            Assert.Contains("whole description", finding.Message);
        }

        [Fact]
        public void InspectionProblemsAreFindingsToo()
        {
            var validation = Validate(P("<$1x>") + P("<@end>") + P("<@if Invoice.BillTo>"));

            Assert.Equal(3, validation.Findings.Count);
            Assert.Equal(FindingKind.MalformedTag, validation.Findings[0].Kind);
            Assert.Equal(FindingKind.UnbalancedBlock, validation.Findings[1].Kind);
            Assert.Equal(FindingKind.UnbalancedBlock, validation.Findings[2].Kind);
            Assert.Contains("never ends", validation.Findings[2].Message);
        }

        [Fact]
        public void FindingsComeInDocumentOrder()
        {
            var body = P("<$Invoice.Nubmer>") + P("<$Invoice.Nubmer2>");
            var styles = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P("<$Invoice.Nubmer3>") + "</style:header></style:master-page></office:master-styles>";
            var settings = new Dictionary<string, string> { ["colour"] = "red" };

            var validation = new OpenDocumentTemplate(Odt.Build(body, masterStyles: styles, settings: settings)).Validate(Root);

            Assert.Equal(new[] { "content.xml", "content.xml", "styles.xml", "meta.xml" }, validation.Findings.Select(v => v.Location.Part));
        }

        [Fact]
        public void APackageThatIsNoTemplateThrows()
        {
            byte[] withoutContent;
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    using (var writer = new StreamWriter(zip.CreateEntry("mimetype").Open()))
                    {
                        writer.Write(Odt.MediaType);
                    }
                }

                withoutContent = stream.ToArray();
            }

            Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(new byte[] { 1, 2, 3 }).Validate(Root));
            Assert.Throws<InvalidDataException>(() => new OpenDocumentTemplate(withoutContent).Validate(Root));
            Assert.Throws<ArgumentNullException>(() => new OpenDocumentTemplate(Odt.Build(P("x"))).Validate(null));
        }

        [Fact]
        public void ArgumentsAreThePublicPropertiesOfTheModelByExactName()
        {
            var shape = ReflectionShape.Arguments<Model>();

            Assert.Equal("Model", shape.Name);
            Assert.Equal(new[] { "Invoice", "Logo", "Parties" }, shape.Members);
            Assert.Equal("Invoice", shape.Member("Invoice").Name);
            Assert.Null(shape.Member("invoice"));
            Assert.Null(shape.Member("Secret"));
            Assert.Null(shape.Member("Computed"));
            Assert.Empty(shape.Variants);
        }

        [Fact]
        public void TheElseOfAnIfDefSeesTheVariantsWithoutTheMember()
        {
            var body =
                P("<@ifdef Invoice.BillTo.FirstName>") + P("<$Invoice.BillTo.FirstName>") +
                P("<@else>") + P("<$Invoice.BillTo.VatNumber>") + P("<$Invoice.BillTo.FirstName>") +
                P("<@end>");

            var validation = Validate(body, root: PolymorphicRoot());

            var finding = Assert.Single(validation.Findings);
            Assert.Equal("Organisation has no member FirstName", finding.Message);
            Assert.Equal(5, finding.Location.Paragraph);
        }

        [Fact]
        public void TheElseOfAnIfSeesTheWholeShape()
        {
            var body = P("<@if Invoice.BillTo.FirstName>") + P("<$Invoice.BillTo.FirstName>") + P("<@else>") + P("<$Invoice.BillTo.VatNumber>") + P("<@end>");

            var validation = Validate(body, root: PolymorphicRoot());

            var finding = Assert.Single(validation.Findings);
            Assert.StartsWith("VatNumber is a member of Organisation but not of every Party", finding.Message);
            Assert.Equal(4, finding.Location.Paragraph);
        }

        [Fact]
        public void NotSwapsTheBranches()
        {
            var body =
                P("<@ifdef not Invoice.BillTo.FirstName>") + P("<$Invoice.BillTo.VatNumber>") +
                P("<@else>") + P("<$Invoice.BillTo.FirstName>") +
                P("<@end>") +
                P("<@if not Invoice.BillTo.FirstName>") + P("<$Invoice.BillTo.Name>") +
                P("<@else>") + P("<$Invoice.BillTo.FirstName>") +
                P("<@end>");

            Assert.Empty(Validate(body, root: PolymorphicRoot()).Findings);
        }

        [Fact]
        public void AnElseWithATestNarrowsItsOwnBranch()
        {
            var body =
                P("<@ifdef Invoice.BillTo.FirstName>") + P("<$Invoice.BillTo.FirstName>") +
                P("<@else ifdef Invoice.BillTo.VatNumber>") + P("<$Invoice.BillTo.VatNumber>") + P("<$Invoice.BillTo.FirstName>") +
                P("<@else>") + P("<$Invoice.BillTo.Name>") + P("<$Invoice.BillTo.VatNumber>") +
                P("<@end>");

            var validation = Validate(body, root: PolymorphicRoot());

            // The last else is checked as if both tests had failed; VatNumber is a member of Organisation itself, so the first test leaves it in
            var finding = Assert.Single(validation.Findings);
            Assert.Equal("Organisation has no member FirstName", finding.Message);
            Assert.Equal(5, finding.Location.Paragraph);
        }

        [Fact]
        public void AnElseIfOnAnUnknownMemberIsReportedOnTheElseTag()
        {
            var validation = Validate(P("<@if Invoice.BillTo>") + P("<@else if Invoice.Nope>") + P("<@else ifdef Invoice.Nope2>") + P("<@end>"));

            Assert.Equal(2, validation.Findings.Count);
            Assert.Equal("<@else if Invoice.Nope>", validation.Findings[0].Text);
            Assert.Equal("Invoice has no member Nope", validation.Findings[0].Message);
            Assert.Equal("<@else ifdef Invoice.Nope2>", validation.Findings[1].Text);
        }

        [Fact]
        public void APictureInAnElseBranchIsCheckedInThatBranch()
        {
            var body =
                P("<@ifdef Invoice.BillTo.Photo>") + P(Odt.Frame("A", "Pictures/a.png", "<$Invoice.BillTo.Photo>")) +
                P("<@else>") + P(Odt.Frame("B", "Pictures/b.png", "<$Invoice.BillTo.Logo>")) + P(Odt.Frame("C", "Pictures/c.png", "<$Invoice.BillTo.Photo>")) +
                P("<@end>");

            var validation = Validate(body, root: PolymorphicRoot());

            var finding = Assert.Single(validation.Findings);
            Assert.Equal("<$Invoice.BillTo.Photo>", finding.Text);
            Assert.Equal("Organisation has no member Photo", finding.Message);
            Assert.Equal(5, finding.Location.Paragraph);
        }

        [Fact]
        public void AnElseInTheParagraphOfTheBeginTagIsAnError()
        {
            var body = "<text:p>" + Odt.Placeholder("<@if Invoice.BillTo>") + Odt.Placeholder("<@else>") + "</text:p>" + P("<$Invoice.Number>") + P("<@end>");

            var finding = Assert.Single(Validate(body).Findings);
            Assert.Equal(FindingKind.BlockStructure, finding.Kind);
            Assert.Equal("<@if Invoice.BillTo>", finding.Text);
            Assert.Contains("give the else a paragraph of its own", finding.Message);
        }

        [Fact]
        public void OtherTextInAnElseParagraphIsAWarning()
        {
            var body = P("<@if Invoice.BillTo>") + P("<$Invoice.Number>") + "<text:p>otherwise " + Odt.Placeholder("<@else>") + "</text:p>" + P("<$Invoice.Number>") + P("<@end>");

            var finding = Assert.Single(Validate(body).Findings);
            Assert.Equal(FindingKind.MixedText, finding.Kind);
            Assert.Equal("<@else>", finding.Text);
            Assert.Contains("'otherwise' with it", finding.Message);
        }

        [Fact]
        public void AnElseInsideOneParagraphIsFine()
        {
            var body = "<text:p>" + Odt.Placeholder("<@if Invoice.BillTo>") + Odt.Placeholder("<$Invoice.BillTo.Name>") + Odt.Placeholder("<@else>") + "nobody" + Odt.Placeholder("<@end>") + "</text:p>";

            Assert.Empty(Validate(body).Findings);
        }

        [Fact]
        public void AnElseThatBelongsToNoConditionIsAnUnbalancedBlock()
        {
            var validation = Validate(P("<@else>") + P("<@for line Invoice.Lines>") + P("<@else>") + P("<@end>"));

            Assert.Equal(2, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.UnbalancedBlock, v.Kind));
            Assert.Contains("belongs to no condition", validation.Findings[0].Message);
            Assert.Contains("sits in a loop", validation.Findings[1].Message);
        }

        [Fact]
        public void ASeparatorOnALoopInParagraphsOrRowsOfItsOwnIsAWarning()
        {
            var body =
                P("<@for line Invoice.Lines separator=\", \">") + P("<$line.Product>") + P("<@end>") +
                Table(Row(Cell(P("<@for line Invoice.Lines separator=\", \">"))) + Row(Cell(P("<$line.Product>"))) + Row(Cell(P("<@end>")))) +
                "<text:p>" + Odt.Placeholder("<@for line Invoice.Lines separator=\", \">") + Odt.Placeholder("<$line.Product>") + Odt.Placeholder("<@end>") + "</text:p>";

            var validation = Validate(body);

            Assert.Equal(2, validation.Findings.Count);
            Assert.All(validation.Findings, v => Assert.Equal(FindingKind.SeparatorIgnored, v.Kind));
            Assert.All(validation.Findings, v => Assert.Equal(FindingSeverity.Warning, v.Severity));
            Assert.Contains("this loop's tags sit in paragraphs of their own", validation.Findings[0].Message);
            Assert.Contains("this loop's tags sit in rows of their own", validation.Findings[1].Message);
            Assert.True(validation.IsValid);
        }

        [Fact]
        public void MapsAreCheckedWithTheBindingsThatUseThem()
        {
            var body =
                Odt.Map("Titles", "true|CREDIT NOTE", "default|INVOICE") +
                Odt.Map("Unused", "a|A", "default|B") +
                Odt.Map("NoDefault", "a|A") +
                P("<$Invoice.Number map=\"Titles\" blank=\"none\">") + P("<$Invoice.Number map=\"NoDefault\">") + P("<$Invoice.Number map=\"Nope\">");

            var validation = Validate(body);

            Assert.Equal(3, validation.Findings.Count);
            Assert.Equal(FindingKind.UnusedMap, validation.Findings[0].Kind);
            Assert.Equal(FindingSeverity.Warning, validation.Findings[0].Severity);
            Assert.Equal("<@map Unused>", validation.Findings[0].Text);
            Assert.Equal("no binding prints through the map 'Unused': use <$path map=\"Unused\">", validation.Findings[0].Message);
            Assert.Equal(7, validation.Findings[0].Location.Paragraph);
            Assert.Equal(FindingKind.MapWithoutDefault, validation.Findings[1].Kind);
            Assert.Equal("the map 'NoDefault' has no default row: a value it has no row for prints as it is", validation.Findings[1].Message);
            Assert.Equal(FindingKind.UnknownMap, validation.Findings[2].Kind);
            Assert.Equal(FindingSeverity.Error, validation.Findings[2].Severity);
            Assert.Equal("no map 'Nope' in the template: a map is a table whose first cell holds <@map Nope>", validation.Findings[2].Message);
            Assert.False(validation.IsValid);
        }

        [Fact]
        public void FindingsWithTheSameValuesAreEqual()
        {
            var body = P("<$Invoice.Nope>") + P("<@for line Invoice.Lines sort=\"Nope\">") + P("<@end>");

            var first = Validate(body).Findings;
            var second = Validate(body).Findings;

            // A finding is a value: two validations of the same template report findings that are equal, location included
            Assert.Equal(2, first.Count);
            Assert.Equal(first, second);
            Assert.Equal(first[0].Location, second[0].Location);
        }

        private static TemplateValidation Validate(string body, IDictionary<string, string> settings = null, IShape root = null) =>
            new OpenDocumentTemplate(Odt.Build(body, settings: settings)).Validate(root ?? Root);

        private static string P(string content) => "<text:p>" + (content.StartsWith("<") && !content.StartsWith("<draw:") ? Odt.Placeholder(content) : content) + "</text:p>";

        private static string Tags(params string[] tags) => "<text:p>" + string.Join(" ", tags.Select(Odt.Placeholder)) + "</text:p>";

        private static string Table(string rows) => "<table:table table:name=\"T\">" + rows + "</table:table>";

        private static string Row(string cells) => "<table:table-row>" + cells + "</table:table-row>";

        private static string Cell(string content) => "<table:table-cell>" + content + "</table:table-cell>";

        /// <summary>A root whose BillTo is a Party with two variants, Person and Organisation, as Allors meta would describe it.</summary>
        private static IShape PolymorphicRoot()
        {
            var text = new TestShape("String", ValueKind.Text);
            var picture = new TestShape("Byte[]", ValueKind.Picture);
            var person = new TestShape("Person");
            var organisation = new TestShape("Organisation");
            var party = new TestShape("Party", person, organisation);
            person.Add("Name", text).Add("FirstName", text).Add("LastName", text).Add("Photo", picture).Add("Children", new TestShape("Persons", ValueKind.Collection, person));
            organisation.Add("Name", text).Add("VatNumber", text).Add("Logo", picture);
            party.Add("Name", text);
            var invoice = new TestShape("Invoice").Add("Number", text).Add("BillTo", party);
            return new TestShape("Model").Add("Invoice", invoice).Add("Parties", new TestShape("Parties", ValueKind.Collection, party));
        }

        private sealed class TestShape : IShape
        {
            private readonly Dictionary<string, IShape> members = new Dictionary<string, IShape>();
            private readonly IShape[] variants;
            private readonly IShape element;

            public TestShape(string name, params IShape[] variants)
            {
                this.Name = name;
                this.variants = variants;
            }

            public TestShape(string name, ValueKind kind, IShape element = null)
                : this(name)
            {
                this.Kind = kind;
                this.element = element;
            }

            public string Name { get; }

            public IEnumerable<string> Members => this.members.Keys;

            public ValueKind Kind { get; } = ValueKind.Object;

            public IShape Element => this.element;

            public IEnumerable<IShape> Variants => this.variants;

            public IShape Member(string name) => this.members.TryGetValue(name, out var member) ? member : null;

            public TestShape Add(string name, IShape shape)
            {
                this.members[name] = shape;
                return this;
            }
        }

        public class Model
        {
            public Invoice Invoice { get; set; }

            public byte[] Logo { get; set; }

            public Party[] Parties { get; set; }

            public string Computed() => "x";

            internal string Secret { get; set; }
        }

        public class Invoice
        {
            public string Number { get; set; }

            public DateTime Date { get; set; }

            public decimal Total { get; set; }

            public bool Paid { get; set; }

            public Line[] Lines { get; set; }

            public Party BillTo { get; set; }
        }

        public class Line
        {
            public string Product { get; set; }

            public decimal Quantity { get; set; }

            public string[] Tags { get; set; }

            public byte[] Picture { get; set; }
        }

        public class Party
        {
            public string Name { get; set; }
        }
    }
}
