// <copyright file="RendererValidationTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Validation.Tests
{
    using Allors.Document.Models;
    using Allors.Document.OpenDocument;
    using Allors.Document.Reflection;
    using Allors.Document.Validation;
    using Allos.Document.OpenDocument.Tests;
    using Allos.Document.Rendering.Tests;
    using Xunit;

    /// <summary>Validate checks a format attribute against the renderers the renders will carry: a binding's names a text renderer, a pattern or a format string; a picture tag's names a picture renderer.</summary>
    public class RendererValidationTests
    {
        private static readonly IShape Root = ReflectionShape.Arguments<Model>();

        private static readonly IRenderer Words = new TextRendererStub("words", (value, text, formatting) => text);

        private static readonly IRenderer Barcode = new PictureRendererStub("code128", (value, frame) => null);

        [Theory]
        [InlineData("upper")]
        [InlineData("LOWER")]
        [InlineData(" cap ")]
        [InlineData("date")]
        [InlineData("N2")]
        [InlineData("yyyy-MM-dd")]
        public void AFormatMayNameABuiltInTextRendererAPatternOrAFormatString(string format) => Assert.Empty(Validate(P("<$Name format=\"" + format + "\">")).Findings);

        [Fact]
        public void ARegisteredTextRendererIsKnownWhenTheRegistryIsPassed()
        {
            Assert.Empty(Validate(P("<$Name format=\"words\">"), new Renderers { Words }).Findings);
            Assert.Empty(Validate(P("<$Name format=\"WORDS\">"), new Renderers { Words }).Findings);
        }

        [Fact]
        public void AFormatThatIsNoRendererAndNoPatternListsTheTextRenderers()
        {
            var alone = Assert.Single(Validate(P("<$Name format=\"Q\">")).Findings);
            Assert.Equal(FindingKind.BadPattern, alone.Kind);
            Assert.Equal(FindingSeverity.Error, alone.Severity);
            Assert.Equal("'Q' is not a format: use a .NET format string, date, time, datetime or number for the document's pattern, or a renderer: upper, lower or cap", alone.Message);
            Assert.Equal("<$Name format=\"Q\">", alone.Text);

            var withWords = Assert.Single(Validate(P("<$Name format=\"Q\">"), new Renderers { Words }).Findings);
            Assert.Equal("'Q' is not a format: use a .NET format string, date, time, datetime or number for the document's pattern, or a renderer: upper, lower, cap or words", withWords.Message);
        }

        [Fact]
        public void APictureRendererOnABindingIsABadValue()
        {
            var validation = Validate(P("<$Name format=\"code128\">"), new Renderers { Barcode });

            var finding = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.BadValue, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("format=\"code128\" renders a picture, not text: use upper, lower or cap, a .NET format string, or date, time, datetime or number for the document's pattern", finding.Message);
            Assert.Equal("<$Name format=\"code128\">", finding.Text);
            Assert.False(validation.IsValid);
        }

        [Fact]
        public void APictureTagsFormatMustNameAPictureRenderer()
        {
            var renderers = new Renderers { Barcode };

            Assert.Empty(Validate(P(Odt.Picture("<$Logo format=\"CODE128\" fit=\"fill\" empty=\"remove\">")), renderers).Findings);

            var upper = Assert.Single(Validate(P(Odt.Picture("<$Logo format=\"upper\">")), renderers).Findings);
            Assert.Equal(FindingKind.BadValue, upper.Kind);
            Assert.Equal(FindingSeverity.Error, upper.Severity);
            Assert.Equal("format=\"upper\" renders text, not a picture: use code128", upper.Message);
            Assert.Equal("<$Logo format=\"upper\">", upper.Text);

            var nope = Assert.Single(Validate(P(Odt.Picture("<$Logo format=\"nope\">")), renderers).Findings);
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
        public void ARendererThatIsTextAndPictureServesBothTags()
        {
            var renderers = new Renderers { new BothRendererStub("qr", (value, text, formatting) => text, (value, frame) => null) };

            Assert.Empty(Validate(P("<$Name format=\"qr\">") + P(Odt.Picture("<$Logo format=\"qr\">")), renderers).Findings);
        }

        [Fact]
        public void TheTwoArgumentValidateAndANullRegistryCheckAgainstTheBuiltInsAlone()
        {
            var body = P("<$Name format=\"upper\">") + P(Odt.Picture("<$Logo format=\"code128\">"));

            var twoArguments = OpenDocumentTemplate.Validate(Odt.Build(body), Root);
            var nullRegistry = OpenDocumentTemplate.Validate(Odt.Build(body), Root, null);

            Assert.Equal("format=\"code128\" names no picture renderer: none is registered", Assert.Single(twoArguments.Findings).Message);
            Assert.Equal("format=\"code128\" names no picture renderer: none is registered", Assert.Single(nullRegistry.Findings).Message);
        }

        [Fact]
        public void APictureInAHeaderIsCheckedToo()
        {
            var styles = "<office:master-styles><style:master-page style:name=\"Standard\"><style:header>" + P(Odt.Picture("<$Logo format=\"nope\">")) + "</style:header></style:master-page></office:master-styles>";

            var validation = OpenDocumentTemplate.Validate(Odt.Build(P("x"), masterStyles: styles), Root, new Renderers { Barcode });

            var finding = Assert.Single(validation.Findings);
            Assert.Equal("styles.xml", finding.Location.Part);
            Assert.Equal("format=\"nope\" names no picture renderer: use code128", finding.Message);
        }

        private static TemplateValidation Validate(string body, Renderers renderers = null) => OpenDocumentTemplate.Validate(Odt.Build(body), Root, renderers);

        private static string P(string content) => "<text:p>" + (content.StartsWith("<") && !content.StartsWith("<draw:") ? Odt.Placeholder(content) : content) + "</text:p>";

        public class Model
        {
            public string Name { get; set; }

            public decimal Amount { get; set; }

            public byte[] Logo { get; set; }
        }
    }
}
