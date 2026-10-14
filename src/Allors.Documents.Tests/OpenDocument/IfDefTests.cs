// <copyright file="IfDefTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System.Collections.Generic;
    using Allors.Document.OpenDocument;
    using Allors.Document.Tags;
    using Xunit;

    /// <summary>
    /// Until 3.0 the StringTemplate engine renders &lt;@ifdef&gt; as &lt;@if&gt;: it cannot tell a missing member from a null one.
    /// The exact semantics, keep the block whenever the member exists, come with the 3.0 renderer.
    /// </summary>
    public class IfDefTests
    {
        private static readonly byte[] Template = Odt.Build(
            "<text:p>" + Odt.Placeholder("<@ifdef Name>") + "</text:p>" +
            "<text:p>Name: " + Odt.Placeholder("<$Name>") + "</text:p>" +
            "<text:p>" + Odt.Placeholder("<@end>") + "</text:p>" +
            "<text:p>after</text:p>");

        [Fact]
        public void ADefinedConditionIsABlock()
        {
            var inspection = OpenDocumentTemplate.Inspect(Template);

            var block = Assert.Single(inspection.Blocks);
            Assert.Equal(TagKind.IfDef, block.Tag.Kind);
            Assert.Equal("Name", block.Tag.Path);
            Assert.NotNull(block.End);
            Assert.Empty(inspection.Problems);
        }

        [Fact]
        public void UnderTheStringTemplateEngineItKeepsTheBlockForAValue()
        {
            var document = new OpenDocumentTemplate(Template, "Name").Render(new Dictionary<string, object> { ["Name"] = "John" });

            Assert.Equal("Name: John\nafter", Odt.Paragraphs(Odt.Content(document)));
        }

        [Fact]
        public void UnderTheStringTemplateEngineItDropsTheBlockForNull()
        {
            var document = new OpenDocumentTemplate(Template, "Name").Render(new Dictionary<string, object> { ["Name"] = null });

            Assert.Equal("after", Odt.Paragraphs(Odt.Content(document)));
        }
    }
}
