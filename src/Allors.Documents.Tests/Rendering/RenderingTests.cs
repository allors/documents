// <copyright file="RenderingTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Allors.Document.Models;
    using Allors.Document.OpenDocument;
    using Allors.Document.Reflection;
    using Allors.Document.Validation;
    using Xunit;
    using static Allos.Document.Rendering.Tests.Render;

    /// <summary>Bindings under the resolver engine: paths, scopes, formatting and what gets reported.</summary>
    public class RenderingTests
    {
        [Fact]
        public void ValuesPrintAtTheirPaths()
        {
            var model = new Dictionary<string, object>
            {
                ["Number"] = "2026-001",
                ["Customer"] = new Dictionary<string, object> { ["Name"] = "Acme", ["Address"] = new Dictionary<string, object> { ["City"] = "Ghent" } },
            };

            var text = Text(P("Invoice ", "<$Number>", " for ", "<$Customer.Name>", " in ", "<$Customer.Address.City>"), model);

            Assert.Equal("Invoice 2026-001 for Acme in Ghent", text);
        }

        [Fact]
        public void ANullValuePrintsBlankWithoutAFinding()
        {
            var result = Document(P("[", "<$Comment>", "]"), new Dictionary<string, object> { ["Comment"] = null });

            Assert.Equal("[]", Paragraphs(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ANullBeforeTheEndOfAPathPrintsBlankWithoutAFinding()
        {
            var result = Document(P("[", "<$Customer.Address.City>", "]"), new Dictionary<string, object> { ["Customer"] = null });

            Assert.Equal("[]", Paragraphs(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void AnUnknownMemberPrintsBlankAndIsReportedOncePerTag()
        {
            var model = new Dictionary<string, object>
            {
                ["Invoice"] = new Dictionary<string, object> { ["Number"] = "1" },
                ["Lines"] = new object[] { "a", "b", "c" },
            };
            var body = P("Total ", "<$Invoice.Nubmer>") + P("<@for line Lines>") + P("<$line>", " ", "<$Invoce.Number>") + P("<@end>");

            var result = Document(body, model);

            Assert.Equal("Total \na \nb \nc ", Paragraphs(result));
            Assert.Equal(2, result.Findings.Count);

            var nested = result.Findings[0];
            Assert.Equal(FindingKind.UnknownMember, nested.Kind);
            Assert.Equal(FindingSeverity.Error, nested.Severity);
            Assert.Equal("<$Invoice.Nubmer>", nested.Text);
            Assert.Equal("Invoice.Nubmer", nested.Path);
            Assert.Equal("Invoice has no member Nubmer", nested.Message);
            Assert.Equal("content.xml", nested.Location.Part);
            Assert.Equal(1, nested.Location.Paragraph);
            Assert.Equal("Total <$Invoice.Nubmer>", nested.Location.ParagraphText);

            var root = result.Findings[1];
            Assert.Equal("the model has no member Invoce", root.Message);
            Assert.Equal("Invoce.Number", root.Path);
            Assert.Equal(3, root.Location.Paragraph);
        }

        [Fact]
        public void AMemberOfAScalarIsUnknown()
        {
            var result = Document(P("<$Count.Length>"), new Dictionary<string, object> { ["Count"] = 42 });

            Assert.Equal(string.Empty, Paragraphs(result));
            Assert.Equal("Count has no member Length", Assert.Single(result.Findings).Message);
        }

        [Fact]
        public void ALoopVariableShadowsARootMemberAndIsItselfAValue()
        {
            var model = new Dictionary<string, object> { ["item"] = "root", ["Items"] = new[] { "x", "y" } };

            var text = Text(P("<$item>") + P("<@for item Items>") + P("<$item>") + P("<@end>") + P("<$item>"), model);

            Assert.Equal("root\nx\ny\nroot", text);
        }

        [Fact]
        public void TheCounterCountsFromOneInTheLoopsOrder()
        {
            var model = new Dictionary<string, object> { ["Items"] = new object[] { Item("b"), Item("c"), Item("a") } };

            var text = Text(P("<@for item Items index=\"n\" sort=\"Name\">") + P("<$n>", ". ", "<$item.Name>") + P("<@end>"), model);

            Assert.Equal("1. a\n2. b\n3. c", text);
        }

        [Fact]
        public void TheCounterPrintsAsAPlainIntegerUnlessTheTagFormatsIt()
        {
            var settings = new RenderSettings { Number = "N2" };
            var model = new Dictionary<string, object> { ["Items"] = new object[] { Item("a", 1), Item("b", 2) } };
            var body = P("<@for item Items index=\"n\">") + P("<$n>", " ", "<$n format=\"00\">", " ", "<$n format=\"number\">", " ", "<$item.Id>") + P("<@end>");

            var text = Text(body, model, settings);

            // The number pattern is for the model's numbers; the counter takes it only when its tag asks for it
            Assert.Equal("1 01 1.00 1.00\n2 02 2.00 2.00", text);
        }

        [Fact]
        public void NestedLoopsSeeOuterVariablesAndCounters()
        {
            var model = new Dictionary<string, object>
            {
                ["Groups"] = new object[]
                {
                    Item("G1", items: new object[] { Item("x"), Item("y") }),
                    Item("G2", items: new object[] { Item("z") }),
                },
            };
            var body =
                P("<@for g Groups index=\"gi\">") +
                P("<@for item g.Items index=\"ii\">") +
                P("<$gi>", ".", "<$ii>", " ", "<$g.Name>", "/", "<$item.Name>") +
                P("<@end>") +
                P("<@end>");

            var text = Text(body, model);

            Assert.Equal("1.1 G1/x\n1.2 G1/y\n2.1 G2/z", text);
        }

        [Fact]
        public void TheDefaultsAreTheInvariantCultureAndPlainText()
        {
            var model = new Dictionary<string, object>
            {
                ["Amount"] = 1234.5m,
                ["When"] = new DateTime(2026, 10, 12, 14, 30, 0),
                ["Day"] = new DateTime(2026, 10, 12),
                ["Paid"] = true,
                ["Count"] = 42,
                ["Colour"] = ConsoleColor.Red,
            };

            var text = Text(P("<$Amount>") + P("<$When>") + P("<$Day>") + P("<$Paid>") + P("<$Count>") + P("<$Colour>"), model);

            Assert.Equal("1234.5\n10/12/2026 14:30:00\n10/12/2026 00:00:00\ntrue\n42\nRed", text);
        }

        [Fact]
        public void TheCallerSettingsFormatValues()
        {
            var settings = new RenderSettings
            {
                Culture = CultureInfo.GetCultureInfo("en-GB"),
                Number = "N2",
                Date = "dd-MMM-yyyy",
                DateTime = "yyyy-MM-dd HH:mm",
                True = "yes",
                False = "no",
            };
            var model = new Dictionary<string, object>
            {
                ["Amount"] = 1234.5m,
                ["Day"] = new DateTime(2026, 10, 12),
                ["When"] = new DateTime(2026, 10, 12, 14, 30, 0),
                ["Paid"] = false,
            };

            var text = Text(P("<$Amount>") + P("<$Day>") + P("<$When>") + P("<$Paid>"), model, settings);

            Assert.Equal("1,234.50\n12-Oct-2026\n2026-10-12 14:30\nno", text);
        }

        [Fact]
        public void TheTemplateSettingsWinOverTheCallerAndTagAttributesWinOverBoth()
        {
            var documentSettings = new Dictionary<string, string> { ["number"] = "0.0", ["true"] = "Ja" };
            var settings = new RenderSettings { Culture = CultureInfo.GetCultureInfo("en-GB"), Number = "N2", True = "yes" };
            var model = new Dictionary<string, object> { ["Amount"] = 1234.5m, ["Paid"] = true };
            var body = P("<$Amount>") + P("<$Amount format=\"0.000\">") + P("<$Amount culture=\"nl-BE\">") + P("<$Paid>") + P("<$Paid true=\"oui\">");

            var text = Text(body, model, settings, documentSettings);

            Assert.Equal("1234.5\n1234.500\n1234,5\nJa\noui", text);
        }

        [Fact]
        public void ANamedFormatUsesTheTemplatesPatternForThatKind()
        {
            var documentSettings = new Dictionary<string, string> { ["date"] = "yyyy-MM-dd", ["time"] = "HH:mm", ["datetime"] = "dd/MM/yyyy HH:mm", ["number"] = "0.00" };
            var model = new Dictionary<string, object> { ["When"] = new DateTime(2026, 10, 12, 14, 30, 0), ["Count"] = 7 };
            var body = P("<$When>") + P("<$When format=\"date\">") + P("<$When format=\"time\">") + P("<$Count format=\"number\">");

            var text = Text(body, model, documentSettings: documentSettings);

            Assert.Equal("12/10/2026 14:30\n2026-10-12\n14:30\n7.00", text);
        }

        [Fact]
        public void AResolverFormatsItsOwnValueTypes()
        {
            var model = new Dictionary<string, object> { ["Total"] = new Money(10m, "EUR") };
            var settings = new RenderSettings { Resolver = new MoneyResolver(), Culture = CultureInfo.GetCultureInfo("nl-BE") };

            var text = Text(P("<$Total>") + P("<$Total.Currency>"), model, settings);

            Assert.Equal("10,00 EUR\nEUR", text);
        }

        [Fact]
        public void TheModelMayBeAnObjectWhenTheResolverReadsIt()
        {
            var model = new Invoice { Number = "7", Lines = new[] { new Line { Product = "Bolt", Quantity = 3 }, new Line { Product = "Nut", Quantity = 12 } } };
            var settings = new RenderSettings { Resolver = ReflectionResolver.Instance };
            var body = P("Invoice ", "<$Number>") + P("<@for line Lines sort=\"Quantity desc\">") + P("<$line.Quantity>", " x ", "<$line.product>") + P("<@end>");

            var result = Document(body, model, settings);

            Assert.Equal("Invoice 7\n12 x Nut\n3 x Bolt", Paragraphs(result));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ANullModelIsRefused()
        {
            var template = Template(P("x"));

            Assert.Throws<ArgumentNullException>(() => template.Render(null, new RenderSettings()));
        }

        private sealed class Money
        {
            public Money(decimal amount, string currency)
            {
                this.Amount = amount;
                this.Currency = currency;
            }

            public decimal Amount { get; }

            public string Currency { get; }
        }

        /// <summary>Reads dictionaries, knows <see cref="Money"/> as a value with a member and its own text.</summary>
        private sealed class MoneyResolver : DictionaryResolver
        {
            public override bool TryGetMember(object scope, string name, out object value)
            {
                if (scope is Money money && name == "Currency")
                {
                    value = money.Currency;
                    return true;
                }

                return base.TryGetMember(scope, name, out value);
            }

            public override string Format(object value, Formatting formatting) =>
                value is Money money ? money.Amount.ToString("0.00", formatting.Culture) + " " + money.Currency : base.Format(value, formatting);
        }

        private sealed class Invoice
        {
            public string Number { get; set; }

            public Line[] Lines { get; set; }
        }

        private sealed class Line
        {
            public string Product { get; set; }

            public int Quantity { get; set; }
        }
    }
}
