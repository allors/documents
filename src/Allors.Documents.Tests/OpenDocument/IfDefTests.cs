// <copyright file="IfDefTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Collections.Generic;
    using Allors.Documents;
    using Xunit;

    /// <summary>An ifdef keeps its block whenever the member exists, null included, and drops it in silence when it does not.</summary>
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
        public void ItKeepsTheBlockForAValue()
        {
            var result = new OpenDocumentTemplate(Template).Render(new Dictionary<string, object> { ["Name"] = "John" });

            Assert.Equal("Name: John\nafter", Odt.Paragraphs(Odt.Content(result.Document)));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ItKeepsTheBlockForNull()
        {
            var result = new OpenDocumentTemplate(Template).Render(new Dictionary<string, object> { ["Name"] = null });

            Assert.Equal("Name: \nafter", Odt.Paragraphs(Odt.Content(result.Document)));
            Assert.Empty(result.Findings);
        }

        [Fact]
        public void ItDropsTheBlockForAMissingMemberWithoutAFinding()
        {
            var result = new OpenDocumentTemplate(Template).Render(new Dictionary<string, object>());

            Assert.Equal("after", Odt.Paragraphs(Odt.Content(result.Document)));
            Assert.Empty(result.Findings);
        }
    }
}
