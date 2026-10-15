// <copyright file="ValidationTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Validation.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Allors.Document.Models;
    using Allors.Document.OpenDocument;
    using Allors.Document.Reflection;
    using Allors.Document.Validation;
    using Allos.Document.OpenDocument.Tests;
    using Xunit;

    public class ValidationTests
    {
        private static readonly IShape Root = ReflectionShape.Arguments<Model>();

        [Fact]
        public void ACleanTemplateHasNoFindings()
        {
            var body =
                P("<$Invoice.Number format=\"0000\" culture=\"nl-BE\">") +
                P("<$Invoice.Date format=\"date\">") +
                P("<@if Invoice.BillTo>") + P("<$Invoice.BillTo.Name true=\"Ja\" false=\"Nee\">") + P("<@end>") +
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
            Assert.Equal("a binding has no attribute 'colour': use format, culture, true, false", validation.Findings[0].Message);
            Assert.Equal("a loop has no attribute 'where': use sort or index", validation.Findings[1].Message);
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
            Assert.Equal("a binding has no attribute 'number-culture': use format, culture, true, false", finding.Message);
        }

        [Fact]
        public void AFormatThatNeitherADateNorANumberAcceptsIsAnError()
        {
            var validation = Validate(P("<$Invoice.Number format=\"Q\">") + P("<$Invoice.Number format=\"yyyy\">") + P("<$Invoice.Number format=\"N2\">") + P("<$Invoice.Number format=\"DateTime\">"));

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.BadPattern, finding.Kind);
            Assert.Equal("<$Invoice.Number format=\"Q\">", finding.Text);
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
            Assert.Equal("a binding has no attribute 'fit': use format, culture, true, false", finding.Message);
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

            var validation = OpenDocumentTemplate.Validate(Odt.Build(body, masterStyles: styles, settings: settings), Root);

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

            Assert.Throws<InvalidDataException>(() => OpenDocumentTemplate.Validate(new byte[] { 1, 2, 3 }, Root));
            Assert.Throws<InvalidDataException>(() => OpenDocumentTemplate.Validate(withoutContent, Root));
            Assert.Throws<ArgumentNullException>(() => OpenDocumentTemplate.Validate(Odt.Build(P("x")), null));
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

        private static TemplateValidation Validate(string body, IDictionary<string, string> settings = null, IShape root = null) =>
            OpenDocumentTemplate.Validate(Odt.Build(body, settings: settings), root ?? Root);

        private static string P(string content) => "<text:p>" + (content.StartsWith("<") && !content.StartsWith("<draw:") ? Odt.Placeholder(content) : content) + "</text:p>";

        private static string Tags(params string[] tags) => "<text:p>" + string.Join(" ", tags.Select(Odt.Placeholder)) + "</text:p>";

        private static string Table(string rows) => "<table:table table:name=\"T\">" + rows + "</table:table>";

        private static string Row(string cells) => "<table:table-row>" + cells + "</table:table-row>";

        private static string Cell(string content) => "<table:table-cell>" + content + "</table:table-cell>";

        /// <summary>A root whose BillTo is a Party with two variants, Person and Organisation, as Allors meta would describe it.</summary>
        private static IShape PolymorphicRoot()
        {
            var text = new TestShape("String");
            var person = new TestShape("Person");
            var organisation = new TestShape("Organisation");
            var party = new TestShape("Party", person, organisation);
            person.Add("Name", text).Add("FirstName", text).Add("LastName", text).Add("Children", new TestShape("Persons", isCollection: true, element: person));
            organisation.Add("Name", text).Add("VatNumber", text);
            party.Add("Name", text);
            var invoice = new TestShape("Invoice").Add("Number", text).Add("BillTo", party);
            return new TestShape("Model").Add("Invoice", invoice).Add("Parties", new TestShape("Parties", isCollection: true, element: party));
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

            public TestShape(string name, bool isCollection, IShape element)
                : this(name)
            {
                this.IsCollection = isCollection;
                this.element = element;
            }

            public string Name { get; }

            public IEnumerable<string> Members => this.members.Keys;

            public bool IsCollection { get; }

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
