// <copyright file="SeparatorTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System.Collections.Generic;
    using Allos.Document.OpenDocument.Tests;
    using Xunit;
    using static Allos.Document.Rendering.Tests.Render;

    /// <summary>The separator attribute: text between the items of a loop that sits in one paragraph.</summary>
    public class SeparatorTests
    {
        [Fact]
        public void ASeparatorIsWrittenBetweenTheItemsOfAnInlineLoop()
        {
            var body = P("Tags: ", "<@for t Tags separator=\", \">", "<$t>", "<@end>", ".");

            Assert.Equal("Tags: a, b, c.", Text(body, new Dictionary<string, object> { ["Tags"] = new[] { "a", "b", "c" } }));
            Assert.Equal("Tags: a.", Text(body, new Dictionary<string, object> { ["Tags"] = new[] { "a" } }));
            Assert.Equal("Tags: .", Text(body, new Dictionary<string, object> { ["Tags"] = new string[0] }));
        }

        [Fact]
        public void ASeparatorFollowsTheSortAndKeepsItsSpaces()
        {
            var model = new Dictionary<string, object> { ["Items"] = new object[] { Item("b", 2), Item("a", 1) } };
            var body = P("<@for item Items sort=\"Name\" index=\"n\" separator=\"  \">", "<$n>", "=", "<$item.Name>", "<@end>");

            Assert.Equal("1=a  2=b", Text(body, model));
        }

        [Fact]
        public void ASeparatorInsideATextRunIsInline()
        {
            var body = "<text:p>Tags: <text:span text:style-name=\"T1\">" + Odt.Placeholder("<@for t Tags separator=\"/\">") + Odt.Placeholder("<$t>") + Odt.Placeholder("<@end>") + "</text:span></text:p>";

            Assert.Equal("Tags: a/b", Text(body, new Dictionary<string, object> { ["Tags"] = new[] { "a", "b" } }));
        }

        [Fact]
        public void ASeparatorIsNotWrittenBetweenParagraphsOrRows()
        {
            var model = new Dictionary<string, object> { ["Tags"] = new[] { "a", "b" } };

            Assert.Equal("a\nb", Text(P("<@for t Tags separator=\", \">") + P("<$t>") + P("<@end>"), model));
            Assert.Equal("a\nb", Text(Table(Row(P("<@for t Tags separator=\", \">")), Row(P("<$t>")), Row(P("<@end>"))), model));
        }
    }
}
