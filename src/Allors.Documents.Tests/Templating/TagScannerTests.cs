// <copyright file="TagScannerTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.Templating;

using System.Linq;
using System.Xml.Linq;
using Allors.Documents;
using Allors.Documents.Templating;
using Xunit;

public class TagScannerTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    [Fact]
    public void ScansForBlockAcrossParagraphs()
    {
        var beginParagraph = Paragraph(Placeholder("<@for p People>"));
        var middleParagraph = Paragraph(Placeholder("<$p.FirstName>"));
        var endParagraph = Paragraph(Placeholder("<@end>"));
        var body = new XElement("body", beginParagraph, middleParagraph, endParagraph);

        var result = TagScanner.Scan(new[] { body });

        var pair = Assert.Single(result.Root.Children);
        var forTag = Assert.IsType<ForTag>(pair.Statement);
        Assert.Equal("p", forTag.ItemName);
        Assert.Equal("People", forTag.Collection.ToString());

        Assert.Same(beginParagraph, pair.BeginSibling);
        Assert.Same(endParagraph, pair.EndSibling);
        Assert.Equal(new XNode[] { middleParagraph }, pair.ContentNodes());

        // The binding is inside the block, so it is not collected at this level.
        Assert.Empty(result.Bindings);
    }

    [Fact]
    public void ScansInlineBlockWithinOneParagraph()
    {
        var begin = Placeholder("<@if Person.FirstName>");
        var binding = Placeholder("<$Person.FirstName>");
        var end = Placeholder("<@end>");
        var paragraph = Paragraph(new XText("Hello "), begin, binding, end, new XText("!"));
        var body = new XElement("body", paragraph);

        var result = TagScanner.Scan(new[] { body });

        var pair = Assert.Single(result.Root.Children);
        Assert.IsType<IfTag>(pair.Statement);
        Assert.Same(begin, pair.BeginSibling);
        Assert.Same(end, pair.EndSibling);
        Assert.Equal(new XNode[] { binding }, pair.ContentNodes());
    }

    [Fact]
    public void ScansNestedBlocks()
    {
        var body = new XElement(
            "body",
            Paragraph(Placeholder("<@for p People>")),
            Paragraph(Placeholder("<@for p2 People>")),
            Paragraph(Placeholder("<$p2.FirstName>")),
            Paragraph(Placeholder("<@end>")),
            Paragraph(Placeholder("<@end>")));

        var result = TagScanner.Scan(new[] { body });

        var outer = Assert.Single(result.Root.Children);
        var inner = Assert.Single(outer.Children);
        Assert.IsType<ForTag>(outer.Statement);
        Assert.IsType<ForTag>(inner.Statement);
        Assert.Empty(inner.Children);
    }

    [Fact]
    public void CollectsTopLevelBindingsAndSkipsNestedOnes()
    {
        var body = new XElement(
            "body",
            Paragraph(Placeholder("<$Title>")),
            Paragraph(Placeholder("<@if Person>")),
            Paragraph(Placeholder("<$Person.FirstName>")),
            Paragraph(Placeholder("<@end>")));

        var result = TagScanner.Scan(new[] { body });

        var binding = Assert.Single(result.Bindings);
        Assert.Equal("Title", binding.Path.ToString());
    }

    [Fact]
    public void CollectsTemplateFramesOutsideBlocks()
    {
        var templateFrame = Frame("$Logo");
        var staticFrame = Frame("logo2");
        var body = new XElement(
            "body",
            new XElement("p", templateFrame),
            new XElement("p", staticFrame),
            Paragraph(Placeholder("<@for image Images>")),
            new XElement("p", Frame("$image")),
            Paragraph(Placeholder("<@end>")));

        var result = TagScanner.Scan(new[] { body });

        var frame = Assert.Single(result.Frames);
        Assert.Same(templateFrame, frame.Element);
        Assert.Equal("Logo", frame.NameExpression.ToString());
    }

    [Fact]
    public void UnbalancedEndThrows()
    {
        var body = new XElement("body", Paragraph(Placeholder("<@end>")));

        var exception = Assert.Throws<TemplateException>(() => TagScanner.Scan(new[] { body }, "content.xml"));

        var error = Assert.Single(exception.Errors);
        Assert.Contains("Unbalanced", error.Message);
        Assert.Equal("content.xml", error.Source);
    }

    [Fact]
    public void MissingEndThrows()
    {
        var body = new XElement("body", Paragraph(Placeholder("<@for p People>")));

        var exception = Assert.Throws<TemplateException>(() => TagScanner.Scan(new[] { body }));

        var error = Assert.Single(exception.Errors);
        Assert.Contains("Missing '<@end>'", error.Message);
        Assert.Contains("<@for p People>", error.Message);
    }

    [Fact]
    public void InvalidExpressionIsEnrichedWithSourceAndTag()
    {
        var body = new XElement("body", Paragraph(Placeholder("<@if Person ==>")));

        var exception = Assert.Throws<TemplateException>(() => TagScanner.Scan(new[] { body }, "content.xml"));

        var error = Assert.Single(exception.Errors);
        Assert.Equal("content.xml", error.Source);
        Assert.Equal("<@if Person ==>", error.Tag);
    }

    [Fact]
    public void InvalidForTagThrows()
    {
        var body = new XElement(
            "body",
            Paragraph(Placeholder("<@for People>")),
            Paragraph(Placeholder("<@end>")));

        var exception = Assert.Throws<TemplateException>(() => TagScanner.Scan(new[] { body }));

        Assert.Contains("expected '<@for item collection>'", exception.Message);
    }

    [Fact]
    public void ReservedForItemNameThrows()
    {
        var body = new XElement(
            "body",
            Paragraph(Placeholder("<@for i People>")),
            Paragraph(Placeholder("<@end>")));

        var exception = Assert.Throws<TemplateException>(() => TagScanner.Scan(new[] { body }));

        Assert.Contains("reserved", exception.Message);
    }

    [Fact]
    public void ParenthesizedForTagIsSupported()
    {
        var body = new XElement(
            "body",
            Paragraph(Placeholder("<@for(p) People>")),
            Paragraph(Placeholder("<@end>")));

        var result = TagScanner.Scan(new[] { body });

        var pair = Assert.Single(result.Root.Children);
        var forTag = Assert.IsType<ForTag>(pair.Statement);
        Assert.Equal("p", forTag.ItemName);
        Assert.Equal("People", forTag.Collection.ToString());
    }

    [Fact]
    public void NonTagPlaceholdersAreIgnored()
    {
        var body = new XElement("body", Paragraph(Placeholder("just a placeholder")));

        var result = TagScanner.Scan(new[] { body });

        Assert.Empty(result.Root.Children);
        Assert.Empty(result.Bindings);
    }

    private static XElement Paragraph(params object[] content) => new(Text + "p", content);

    private static XElement Placeholder(string text) => new(Text + "placeholder", text);

    private static XElement Frame(string name) => new(Draw + "frame", new XAttribute(Draw + "name", name));
}
