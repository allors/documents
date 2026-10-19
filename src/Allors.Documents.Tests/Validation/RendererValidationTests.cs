// <copyright file="RendererValidationTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Linq;
    using Allors.Documents;
    using Allors.Documents.Reflection;
    using Xunit;

    /// <summary>
    /// Validate checks a format attribute against the template's renderers before the pattern grammar: a binding's names a text renderer, a
    /// pattern or a format string; a picture tag's names a picture renderer, which draws the frame's picture for a value of any kind.
    /// </summary>
    public class RendererValidationTests
    {
        private static readonly IShape Root = ReflectionShape.Arguments<Model>();

        private static readonly IRenderer Words = new TextRendererStub("words", (value, text, formatting) => text);

        private static readonly IRenderer Barcode = new PictureRendererStub("code128", (value, text, frame) => null);

        [Theory]
        [InlineData("<$Name format=\"upper\">")]
        [InlineData("<$Name format=\"LOWER\">")]
        [InlineData("<$Name format=\" cap \">")]
        [InlineData("<$Amount format=\"upper\">")]
        [InlineData("<$Amount format=\"N2\">")]
        [InlineData("<$Amount format=\"number\">")]
        public void AFormatMayNameABuiltInTextRendererOnAnyKindOrAPatternOfTheValuesKind(string tag) => Assert.Empty(Validate(P(tag)).Findings);

        [Fact]
        public void ARendererOfTheSettingsIsKnownInAnyCase()
        {
            Assert.Empty(Validate(P("<$Name format=\"words\">"), Words).Findings);
            Assert.Empty(Validate(P("<$Name format=\"WORDS\">"), Words).Findings);
        }

        [Fact]
        public void AFormatThatIsNoRendererAndNoPatternListsTheTextRenderers()
        {
            var alone = Assert.Single(Validate(P("<$Name format=\"Q\">"), DictionaryShape.Open).Findings);
            Assert.Equal(FindingKind.BadPattern, alone.Kind);
            Assert.Equal(FindingSeverity.Error, alone.Severity);
            Assert.Equal("'Q' is not a format: use a date or number pattern, date, time, datetime or number for the document's pattern, or a renderer: upper, lower or cap", alone.Message);
            Assert.Equal("<$Name format=\"Q\">", alone.Text);

            var withWords = Assert.Single(Validate(P("<$Name format=\"Q\">"), DictionaryShape.Open, Words).Findings);
            Assert.Equal("'Q' is not a format: use a date or number pattern, date, time, datetime or number for the document's pattern, or a renderer: upper, lower, cap or words", withWords.Message);
        }

        [Fact]
        public void APatternOnTextIsIgnoredAsBefore()
        {
            var finding = Assert.Single(Validate(P("<$Name format=\"uper\">")).Findings);

            Assert.Equal(FindingKind.FormatIgnored, finding.Kind);
            Assert.Equal(FindingSeverity.Warning, finding.Severity);
            Assert.Equal("the format is ignored: 'Name' is text and prints without a pattern", finding.Message);
        }

        [Fact]
        public void APictureRendererOnABindingIsABadValue()
        {
            var validation = Validate(P("<$Name format=\"code128\">"), Barcode);

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.BadValue, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("format=\"code128\" renders a picture, not text: use upper, lower or cap, a pattern, or date, time, datetime or number for the document's pattern", finding.Message);
            Assert.Equal("<$Name format=\"code128\">", finding.Text);
            Assert.False(validation.IsValid);
        }

        [Fact]
        public void APictureTagsFormatMustNameAPictureRenderer()
        {
            Assert.Empty(Validate(P(Odt.Picture("<$Logo format=\"CODE128\" fit=\"fill\" empty=\"remove\">")), Barcode).Findings);

            var upper = Assert.Single(Validate(P(Odt.Picture("<$Logo format=\"upper\">")), Barcode).Findings);
            Assert.Equal(FindingKind.BadValue, upper.Kind);
            Assert.Equal(FindingSeverity.Error, upper.Severity);
            Assert.Equal("format=\"upper\" renders text, not a picture: use code128", upper.Message);
            Assert.Equal("<$Logo format=\"upper\">", upper.Text);

            var nope = Assert.Single(Validate(P(Odt.Picture("<$Logo format=\"nope\">")), Barcode).Findings);
            Assert.Equal(FindingKind.BadValue, nope.Kind);
            Assert.Equal("format=\"nope\" names no picture renderer: use code128", nope.Message);
            Assert.Equal(1, nope.Location.Paragraph);
        }

        [Fact]
        public void WithoutAPictureRendererTheMessageSaysSo()
        {
            var finding = Assert.Single(Validate(P(Odt.Picture("<$Logo format=\"code128\">"))).Findings);

            Assert.Equal(FindingKind.BadValue, finding.Kind);
            Assert.Equal("format=\"code128\" names no picture renderer: none is registered", finding.Message);
        }

        [Fact]
        public void APictureRendererDrawsAValueOfAnyKindAndWithoutOneTheValueMustBeAPicture()
        {
            Assert.Empty(Validate(P(Odt.Picture("<$Amount format=\"code128\">")), Barcode).Findings);

            var plain = Assert.Single(Validate(P(Odt.Picture("<$Amount>")), Barcode).Findings);
            Assert.Equal(FindingKind.NotAPicture, plain.Kind);
            Assert.Equal("'Amount' is a number, not a picture: bind the frame to a member that holds the bytes of a picture", plain.Message);

            // A format that names no picture renderer leaves the frame with the resolver's picture, so the kind is checked as well
            var findings = Validate(P(Odt.Picture("<$Amount format=\"nope\">")), Barcode).Findings;
            Assert.Equal([FindingKind.BadValue, FindingKind.NotAPicture], findings.Select(v => v.Kind));
        }

        [Fact]
        public void ARendererThatIsTextAndPictureServesBothTags()
        {
            var both = new BothRendererStub("qr", (value, text, formatting) => text, (value, text, frame) => null);

            Assert.Empty(Validate(P("<$Name format=\"qr\">") + P(Odt.Picture("<$Logo format=\"qr\">")), both).Findings);
        }

        [Fact]
        public void APictureInAHeaderIsCheckedToo()
        {
            var styles = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P(Odt.Picture("<$Logo format=\"nope\">")) + "</style:header></style:master-page></office:master-styles>";

            var validation = new OpenDocumentTemplate(Odt.Build(P("x"), masterStyles: styles), new RenderSettings { Renderers = [Barcode] }).Validate(Root);

            var finding = Assert.Single(validation.Findings);
            Assert.Equal("styles.xml", finding.Location.Part);
            Assert.Equal("format=\"nope\" names no picture renderer: use code128", finding.Message);
        }

        private static TemplateValidation Validate(string body, params IRenderer[] renderers) => Validate(body, Root, renderers);

        private static TemplateValidation Validate(string body, IShape root, params IRenderer[] renderers) =>
            new OpenDocumentTemplate(Odt.Build(body), new RenderSettings { Renderers = renderers }).Validate(root);

        private static string P(string content) => "<text:p>" + (content.StartsWith("<") && !content.StartsWith("<draw:") ? Odt.Placeholder(content) : content) + "</text:p>";

        public class Model
        {
            public string Name { get; set; }

            public decimal Amount { get; set; }

            public byte[] Logo { get; set; }
        }
    }
}
