// <copyright file="BlankTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System.Collections.Generic;
    using Allors.Document.Validation;
    using Allos.Document.OpenDocument.Tests;
    using Xunit;
    using static Allos.Document.Rendering.Tests.Render;

    /// <summary>The blank attribute: the text a binding prints when what it would print is empty or white space.</summary>
    public class BlankTests
    {
        [Theory]
        [InlineData(null, "none")]
        [InlineData("", "none")]
        [InlineData("  ", "none")]
        [InlineData("x", "x")]
        [InlineData(0, "0")]
        [InlineData(false, "false")]
        public void TheBlankTextPrintsForNullAndWhiteSpaceOnly(object value, string expected)
        {
            var text = Text(P("<$Comment blank=\"none\">"), new Dictionary<string, object> { ["Comment"] = value });

            Assert.Equal(expected, text);
        }

        [Fact]
        public void TheBlankTextPrintsForAPathThatBreaks()
        {
            var nullBefore = Document(P("<$Customer.Name blank='n/a'>"), new Dictionary<string, object> { ["Customer"] = null });
            Assert.Equal("n/a", Paragraphs(nullBefore));
            Assert.Empty(nullBefore.Findings);

            var unknown = Document(P("<$Nope blank='n/a'>"), new Dictionary<string, object>());
            Assert.Equal("n/a", Paragraphs(unknown));
            Assert.Equal(FindingKind.UnknownMember, Assert.Single(unknown.Findings).Kind);
        }

        [Fact]
        public void TheBlankTextPrintsForAnEmptyCollectionAndForAMapOfANull()
        {
            var body = Odt.Map("Words", "default|other") + Tags("<$Items blank=\"none\">", "<$Nothing map=\"Words\" blank=\"none\">");

            var text = Text(body, new Dictionary<string, object> { ["Items"] = new object[0], ["Nothing"] = null });

            Assert.Equal("none none", text);
        }

        [Fact]
        public void TheBlankTextIsWrittenAsTyped()
        {
            var text = Text(P("[", "<$Comment blank=\"a  b\">", "]"), new Dictionary<string, object> { ["Comment"] = null });

            Assert.Equal("[a  b]", text);
        }

        private static string Tags(params string[] tags) => "<text:p>" + Odt.Placeholder(tags[0]) + " " + Odt.Placeholder(tags[1]) + "</text:p>";
    }
}
