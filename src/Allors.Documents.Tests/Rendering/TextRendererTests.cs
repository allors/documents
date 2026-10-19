// <copyright file="TextRendererTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Allors.Documents;
    using Xunit;
    using static Allors.Documents.Tests.Render;

    /// <summary>
    /// The format attribute names a text renderer before it is read as a pattern name or a .NET format string: the resolver formats the value as
    /// it would without a format, and the renderer transforms the text.
    /// </summary>
    public class TextRendererTests
    {
        [Fact]
        public void TheBuiltInsTransformTheFormattedText()
        {
            var body = P("<$Name format=\"upper\">", " ", "<$Name format=\"LOWER\">", " ", "<$Name format=\"cap\">");

            Assert.Equal("VAN EXEM van exem Van Exem", Text(body, new Dictionary<string, object> { ["Name"] = "van Exem" }));
        }

        [Fact]
        public void ARendererGetsTheTextTheResolverFormatsUnderTheOtherSettingsAndItsNameNeverReachesTheResolver()
        {
            var body = P("<$Amount format=\"upper\">", " ", "<$Date format=\"upper\">", " ", "<$Paid format=\"upper\" true=\"ja\">");
            var model = new Dictionary<string, object> { ["Amount"] = 1234.5m, ["Date"] = new DateTime(2026, 10, 14), ["Paid"] = true };
            var settings = new RenderSettings { Culture = CultureInfo.GetCultureInfo("nl-BE"), Number = "N2", Date = "dd-MMM-yyyy" };

            var result = Document(body, model, settings);

            // 1234.5m.ToString("upper") would throw: the name is taken out of the formatting and the number pattern applies
            Assert.Equal("1.234,50 14-OKT-2026 JA", Paragraphs(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ARegisteredRendererIsSelectedByNameInAnyCaseAndMayReplaceABuiltIn()
        {
            var words = new TextRendererStub("words", (value, text, formatting) => (int)value == 2 ? "two" : text);
            var shout = new TextRendererStub("upper", (value, text, formatting) => text + "!");
            var settings = new RenderSettings { Renderers = [words, shout] };
            var body = P("<$Count format=\"Words\">", " ", "<$Name format=\"upper\">");

            Assert.Equal("two Koen!", Text(body, new Dictionary<string, object> { ["Count"] = 2, ["Name"] = "Koen" }, settings));
        }

        [Fact]
        public void ARendererSeesTheValueAndTheEffectiveFormattingWithoutItsOwnName()
        {
            string format = "unset";
            var probe = new TextRendererStub("probe", (value, text, formatting) =>
            {
                format = formatting.Format;
                return value.GetType().Name + " " + formatting.Culture.Name + " " + formatting.True + " " + text;
            });
            var settings = new RenderSettings { Culture = CultureInfo.GetCultureInfo("nl-BE"), Renderers = [probe] };

            Assert.Equal("Decimal nl-BE ja 2,5", Text(P("<$Amount format=\"probe\" true=\"ja\">"), new Dictionary<string, object> { ["Amount"] = 2.5m }, settings));
            Assert.Null(format);
        }

        [Fact]
        public void AMapWinsOverTheRendererUnlessTheMapHasNoRowForTheValue()
        {
            var body = P("<$Kind map=\"Titles\" format=\"upper\">", " ", "<$Other map=\"Titles\" format=\"upper\">") + Odt.Map("Titles", "credit|Credit note");
            var model = new Dictionary<string, object> { ["Kind"] = "credit", ["Other"] = "invoice" };

            var result = Document(body, model);

            Assert.Equal("Credit note INVOICE", Paragraphs(result));
            Assert.Equal(FindingKind.MissingKey, Assert.Single(result.Findings).Kind);
        }

        [Fact]
        public void BlankAppliesToWhatTheRendererGives()
        {
            var settings = new RenderSettings { Renderers = [new TextRendererStub("erase", (value, text, formatting) => string.Empty)] };

            Assert.Equal("n/a", Text(P("<$Name format=\"erase\" blank=\"n/a\">"), new Dictionary<string, object> { ["Name"] = "x" }, settings));
        }

        [Fact]
        public void ALoopCounterWithARendererStaysAPlainInteger()
        {
            var body = P("<@for item Items index=\"n\">", "<$n format=\"upper\">", "<@end>");
            var settings = new RenderSettings { Number = "N2" };

            Assert.Equal("12", Text(body, new Dictionary<string, object> { ["Items"] = new object[] { "a", "b" } }, settings));
        }

        [Fact]
        public void ARendererThatThrowsIsReportedOncePerTagAndTheValuePrintsBlank()
        {
            var settings = new RenderSettings { Renderers = [new TextRendererStub("failing", (value, text, formatting) => throw new InvalidOperationException("boom"))] };
            var body = P("<@for item Items>", "[", "<$item format=\"failing\">", "]", "<@end>");

            var result = Document(body, new Dictionary<string, object> { ["Items"] = new object[] { "a", "b" } }, settings);

            Assert.Equal("[][]", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.RendererFailed, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("the renderer 'failing' failed on 'item' and the value prints blank: boom", finding.Message);
            Assert.Equal("item", finding.Path);
            Assert.Equal("<$item format=\"failing\">", finding.Text);
        }

        [Fact]
        public void APictureRendererOnABindingIsReportedAndTheValuePrintsWithoutIt()
        {
            var settings = new RenderSettings { Renderers = [new PictureRendererStub("code128", (value, text, frame) => null)], Number = "N2" };

            var result = Document(P("<$Amount format=\"code128\">"), new Dictionary<string, object> { ["Amount"] = 2.5m }, settings);

            Assert.Equal("2.50", Paragraphs(result));
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.BadValue, finding.Kind);
            Assert.Equal(FindingSeverity.Error, finding.Severity);
            Assert.Equal("format=\"code128\" renders a picture, not text: use upper, lower or cap, a pattern, or date, time, datetime or number for the document's pattern", finding.Message);
            Assert.Equal("Amount", finding.Path);
        }

        [Fact]
        public void APatternNameOrAFormatStringStillAppliesWhenNoRendererHasTheName()
        {
            var body = P("<$Date format=\"date\">", " ", "<$Date format=\"yyyy\">", " ", "<$Amount format=\"0000\">");
            var model = new Dictionary<string, object> { ["Date"] = new DateTime(2026, 10, 14, 9, 30, 0), ["Amount"] = 7 };
            var settings = new RenderSettings { Date = "dd-MM-yyyy" };

            Assert.Equal("14-10-2026 2026 0007", Text(body, model, settings));
        }

        [Fact]
        public void ARegisteredNameShadowsAPatternName()
        {
            var settings = new RenderSettings { Number = "N2", Renderers = [new TextRendererStub("number", (value, text, formatting) => "<" + text + ">")] };

            Assert.Equal("<2.50>", Text(P("<$Amount format=\"number\">"), new Dictionary<string, object> { ["Amount"] = 2.5m }, settings));
        }

        [Fact]
        public void ARendererSeesNoNullValueAndNoBrokenPath()
        {
            var probe = new TextRendererStub("probe", (value, text, formatting) => "called");
            var settings = new RenderSettings { Renderers = [probe] };

            var result = Document(P("<$Missing format=\"probe\">", "<$Nope format=\"probe\">"), new Dictionary<string, object> { ["Missing"] = null }, settings);

            Assert.Equal(string.Empty, Paragraphs(result));
            Assert.Equal(0, probe.Calls);
            Assert.Equal(FindingKind.UnknownMember, Assert.Single(result.Findings).Kind);
        }

        [Fact]
        public void TheTemplatesRenderersServeEveryRenderOfIt()
        {
            var template = Template(P("<$Name format=\"upper\">"));

            Assert.Equal("A", Paragraphs(template.Render(new Dictionary<string, object> { ["Name"] = "a" })));
            Assert.Equal("B", Paragraphs(template.Render(new Dictionary<string, object> { ["Name"] = "b" })));
        }

        [Fact]
        public void ALaterRendererReplacesAnEarlierOneWithItsNameAndKeepsItsPlace()
        {
            var first = new TextRendererStub("words", (value, text, formatting) => "first");
            var second = new TextRendererStub(" Words ", (value, text, formatting) => "second");
            var template = Template(P("<$Name format=\"words\">"), settings: new RenderSettings { Renderers = [first, second] });

            Assert.Equal("second", Paragraphs(template.Render(new Dictionary<string, object> { ["Name"] = "a" })));
            Assert.Equal(0, first.Calls);
        }

        [Fact]
        public void ANullRendererOrOneWithoutANameIsRefusedWhenTheTemplateIsRead()
        {
            var bytes = Odt.Build(P("x"));

            var nullRenderer = Assert.Throws<ArgumentException>(() => new OpenDocumentTemplate(bytes, new RenderSettings { Renderers = [TextRenderers.Upper, null] }));
            Assert.StartsWith("the renderers hold a null: list renderers only", nullRenderer.Message);

            var noName = Assert.Throws<ArgumentException>(() => new OpenDocumentTemplate(bytes, new RenderSettings { Renderers = [new TextRendererStub(" ", (value, text, formatting) => text)] }));
            Assert.StartsWith("a renderer needs a name, the text a tag selects it by in its format attribute: TextRendererStub has none", noName.Message);
        }
    }
}
