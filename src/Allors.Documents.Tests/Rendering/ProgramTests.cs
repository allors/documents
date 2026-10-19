// <copyright file="ProgramTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Linq;
    using System.Xml;
    using Allors.Documents;
    using Xunit;
    using static Allors.Documents.Tests.Render;

    /// <summary>The program a part becomes: instruction elements where the tags stood, and nothing for a template whose blocks cannot be cut.</summary>
    public class ProgramTests
    {
        private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        [Fact]
        public void TagsBecomeInstructionElementsInTheProgramsNamespace()
        {
            var body =
                P("Dear ", "<$Name>") +
                P("<@for line Lines index=\"n\">") + P("<$line.Amount>") + P("<@end>") +
                P("<@if Flag>") + P("yes") + P("<@end>") +
                P("<@ifdef Flag>") + P("maybe") + P("<@end>");

            var program = Program.Build("content.xml", Odt.Entry(Odt.Build(body), "content.xml").Bytes);

            var instructions = program.Document.GetElementsByTagName("*", Program.Namespace).Cast<XmlElement>().ToList();
            Assert.Equal(["binding", "for", "binding", "if", "ifdef"], instructions.Select(v => v.LocalName));
            Assert.Equal(5, program.Instructions.Count);
            Assert.Empty(program.Document.GetElementsByTagName("placeholder", TextNamespace).Cast<XmlElement>());
            Assert.Equal(4, program.Document.GetElementsByTagName("p", TextNamespace).Count);
            Assert.Empty(program.Findings);

            var loop = program.Instructions[instructions[1]];
            Assert.Equal(TagKind.For, loop.Tag.Kind);
            Assert.Equal("line", loop.Tag.Variable);
            Assert.Equal("n", loop.Tag.Index);
            Assert.Equal(2, loop.Location.Paragraph);
            Assert.Equal("<@for line Lines index=\"n\">", loop.Location.ParagraphText);

            // The loop holds the paragraph between its tags, and that paragraph holds the binding
            var bodyParagraph = Assert.Single(instructions[1].ChildNodes.Cast<XmlNode>());
            Assert.Equal("p", bodyParagraph.LocalName);
            Assert.Same(instructions[2], bodyParagraph.FirstChild);
        }

        [Fact]
        public void AConditionHoldsItsFirstBranchAndAnElseElementPerFurtherBranch()
        {
            var body = P("<@if Flag>") + P("yes") + P("<@else if Other>") + P("maybe") + P("<@else>") + P("no") + P("<@end>") + Odt.Map("M", "a|A");

            var program = Program.Build("content.xml", Odt.Entry(Odt.Build(body), "content.xml").Bytes);

            var condition = Assert.Single(program.Document.GetElementsByTagName("if", Program.Namespace).Cast<XmlElement>());
            Assert.Equal(["p", "else", "else"], condition.ChildNodes.Cast<XmlNode>().Select(v => v.LocalName));
            Assert.Equal("yes", condition.FirstChild.InnerText);
            Assert.Equal("maybe", condition.ChildNodes[1].InnerText);
            Assert.Equal("no", condition.ChildNodes[2].InnerText);

            var elseIf = program.Instructions[(XmlElement)condition.ChildNodes[1]];
            Assert.Equal(TagKind.Else, elseIf.Tag.Kind);
            Assert.Equal("Other", elseIf.Tag.Path);
            Assert.Equal(3, elseIf.Location.Paragraph);
            Assert.False(program.Instructions[(XmlElement)condition.ChildNodes[2]].Tag.HasTest);
            Assert.Equal(3, program.Instructions.Count);

            // The map table leaves the program; only the branches' paragraphs remain
            Assert.Empty(program.Document.GetElementsByTagName("table", "urn:oasis:names:tc:opendocument:xmlns:table:1.0").Cast<XmlElement>());
            Assert.Equal(3, program.Document.GetElementsByTagName("p", TextNamespace).Count);
        }

        [Fact]
        public void ALoopKnowsWhetherItSitsInsideOneParagraph()
        {
            var body = P("<@for a Items>") + P("<$a>") + P("<@end>") + P("<@for b Items>", "<$b>", "<@end>");

            var program = Program.Build("content.xml", Odt.Entry(Odt.Build(body), "content.xml").Bytes);

            var loops = program.Document.GetElementsByTagName("for", Program.Namespace).Cast<XmlElement>().Select(v => program.Instructions[v]).ToList();
            Assert.False(loops[0].Inline);
            Assert.True(loops[1].Inline);
        }

        [Theory]
        [InlineData("<@for line Lines>", "this block never ends")]
        [InlineData("<@end>", "closes nothing")]
        public void AnUnbalancedBlockCannotBeBuilt(string tag, string message)
        {
            var xml = Odt.Entry(Odt.Build(P("x") + P(tag)), "content.xml").Bytes;

            var exception = Assert.Throws<TemplateException>(() => Program.Build("content.xml", xml));

            var finding = Assert.Single(exception.Findings);
            Assert.Equal(FindingKind.UnbalancedBlock, finding.Kind);
            Assert.Equal(tag, finding.Text);
            Assert.Contains(message, finding.Message);
            Assert.Equal(2, finding.Location.Paragraph);
            Assert.Contains(tag, exception.Message);
        }

        [Fact]
        public void AMalformedTagStaysInThePartAndIsAFinding()
        {
            var program = Program.Build("content.xml", Odt.Entry(Odt.Build(P("<@for x>")), "content.xml").Bytes);

            Assert.Single(program.Document.GetElementsByTagName("placeholder", TextNamespace).Cast<XmlElement>());
            Assert.Empty(program.Instructions);
            var finding = Assert.Single(program.Findings);
            Assert.Equal(FindingKind.MalformedTag, finding.Kind);
            Assert.Equal("<@for x>", finding.Text);
        }
    }
}
