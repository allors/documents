// <copyright file="OpenDocumentTemplateTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Allors.Documents;
    using Allors.Documents.Reflection;
    using Xunit;

    /// <summary>
    /// A template over a model type: the render sees the model's public properties by their exact names and reads what is below them
    /// through the reflection resolver, the root and the rules <see cref="ReflectionShape.Arguments{T}"/> gives Validate.
    /// </summary>
    public class OpenDocumentTemplateTests
    {
        private static readonly Model Model = new Model
        {
            Person = new ModelPerson { FirstName = "Jane" },
            People = [new ModelPerson { FirstName = "John" }, new ModelPerson { FirstName = "Jenny" }],
            Images = ["number1", "number2", "number3"],
        };

        [Fact]
        public void TheWriterTemplateOfTheSuiteRenders()
        {
            var template = new OpenDocumentTemplate<Model>(Resource("EmbeddedTemplate.odt"));

            var result = template.Render(Model);

            var paragraphs = Odt.Paragraphs(Odt.Content(result.Document));
            Assert.Contains("Jane", paragraphs);
            Assert.Contains("John", paragraphs);
            Assert.Contains("Jenny", paragraphs);

            // The template asks for a last name the model does not have
            var finding = Assert.Single(result.Findings);
            Assert.Equal(FindingKind.UnknownMember, finding.Kind);
            Assert.Equal("<$p.LastName>", finding.Text);
        }

        [Fact]
        public void TheRootHasThePropertiesAsWrittenAndTheRestFollowsTheReflectionRules()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Person.firstName>") + "</text:p><text:p>" + Odt.Placeholder("<$person.FirstName>") + "</text:p>");
            var template = new OpenDocumentTemplate<Model>(package);

            var result = template.Render(Model);

            // Below the root a name may start with a lower-case letter; the root takes the property names as they are, and Validate judges the same way
            Assert.Equal("Jane\n", Odt.Paragraphs(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal("<$person.FirstName>", finding.Text);
            Assert.Equal("the model has no member person", finding.Message);

            var validation = new OpenDocumentTemplate(package).Validate(ReflectionShape.Arguments<Model>());
            var error = Assert.Single(validation.Findings);
            Assert.Equal(FindingKind.UnknownMember, error.Kind);
            Assert.Equal("<$person.FirstName>", error.Text);
        }

        [Fact]
        public void TheSettingsOfARenderApply()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Total>") + "</text:p>");
            var template = new OpenDocumentTemplate<Invoice>(package);

            var plain = template.Render(new Invoice());
            var formatted = new OpenDocumentTemplate<Invoice>(package, new RenderSettings { Culture = new CultureInfo("nl-BE"), Number = "N2" }).Render(new Invoice());

            Assert.Equal("1234.5", Odt.Paragraphs(Odt.Content(plain.Document)));
            Assert.Equal("1.234,50", Odt.Paragraphs(Odt.Content(formatted.Document)));
        }

        [Fact]
        public void AResolverInTheSettingsReplacesTheReflectionResolver()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Person.FirstName>") + "</text:p>");
            var template = new OpenDocumentTemplate<Model>(package, new RenderSettings { Resolver = DictionaryResolver.Instance });

            var result = template.Render(Model);

            // The dictionary resolver reads the root, which is a dictionary, and nothing of the person, which is not
            Assert.Equal(string.Empty, Odt.Paragraphs(Odt.Content(result.Document)));
            var finding = Assert.Single(result.Findings);
            Assert.Equal("Person has no member FirstName", finding.Message);
        }

        [Fact]
        public void ANullModelIsRefused()
        {
            var template = new OpenDocumentTemplate<Model>(Odt.Build("<text:p>x</text:p>"));

            Assert.Throws<ArgumentNullException>(() => template.Render((Model)null));
        }

        [Fact]
        public void ATemplateRendersAgainToTheSameDocument()
        {
            var template = new OpenDocumentTemplate<Model>(Resource("EmbeddedTemplate.odt"));

            var first = template.Render(Model);
            var second = template.Render(Model);

            Assert.Equal(Odt.Entry(first.Document, "content.xml").Bytes, Odt.Entry(second.Document, "content.xml").Bytes);
            Assert.Equal(Odt.Entry(first.Document, "styles.xml").Bytes, Odt.Entry(second.Document, "styles.xml").Bytes);
        }

        private static byte[] Resource(string name)
        {
            var assembly = typeof(OpenDocumentTemplateTests).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(v => v.EndsWith("." + name));
            using var resource = assembly.GetManifestResourceStream(resourceName);
            using var output = new MemoryStream();
            resource.CopyTo(output);
            return output.ToArray();
        }

        [Fact]
        public void TheResultSplitsItsFindingsIntoErrorsAndWarnings()
        {
            var body = "<text:p>" + Odt.Placeholder("<$Nope>") + "</text:p>" +
                "<text:p>" + Odt.Placeholder("<@for p People sort=\"FirstName\">") + "</text:p><text:p>" + Odt.Placeholder("<$p.FirstName>") + "</text:p><text:p>" + Odt.Placeholder("<@end>") + "</text:p>";
            var template = new OpenDocumentTemplate(Odt.Build(body));
            var model = new Dictionary<string, object> { ["People"] = new object[] { new Dictionary<string, object> { ["FirstName"] = "Jane" }, new Dictionary<string, object> { ["FirstName"] = 7 } } };

            var result = template.Render(model);

            // The unknown member is an error, the loop that cannot be ordered a warning; Findings holds both in document order
            Assert.Equal(2, result.Findings.Count);
            Assert.Equal(FindingKind.UnknownMember, Assert.Single(result.Errors).Kind);
            Assert.Equal(FindingKind.CannotSort, Assert.Single(result.Warnings).Kind);
        }

        [Fact]
        public void ATemplateWhoseBlocksCannotRenderIsReadAndRefusesToRender()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<@if Flag>") + "</text:p><text:p>" + Odt.Placeholder("<$Name>") + "</text:p>");

            var template = new OpenDocumentTemplate(package);

            // The template is read: the inspection lists its tags and the problem, Validate with an open shape reports it without a model; only Render refuses
            Assert.Equal(["Flag", "Name"], template.Inspection.Paths);
            Assert.Single(template.Inspection.Problems);
            Assert.Contains(template.Validate(DictionaryShape.Open).Errors, v => v.Kind == FindingKind.UnbalancedBlock);
            var exception = Assert.Throws<TemplateException>(() => template.Render(new Dictionary<string, object>()));
            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.UnbalancedBlock, finding.Kind);
            Assert.Equal("<@if Flag>", finding.Text);
        }

        [Fact]
        public void TheSettingsBelongToTheTemplate()
        {
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Total>") + "</text:p>");
            var settings = new RenderSettings { Culture = new CultureInfo("nl-BE"), Number = "N2" };

            var template = new OpenDocumentTemplate<Invoice>(package, settings);
            var plain = new OpenDocumentTemplate<Invoice>(package);

            // Every render of a template uses the settings it was read with; a template without settings has the defaults, resolver included
            Assert.Same(settings, template.Settings);
            Assert.Null(plain.Settings.Resolver);
            Assert.Equal("1.234,50", Odt.Paragraphs(Odt.Content(template.Render(new Invoice()).Document)));
            Assert.Equal("1234.5", Odt.Paragraphs(Odt.Content(plain.Render(new Invoice()).Document)));
        }

        private sealed class Invoice
        {
            public decimal Total { get; set; } = 1234.5m;
        }
    }
}
