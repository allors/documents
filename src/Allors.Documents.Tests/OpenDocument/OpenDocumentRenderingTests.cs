// <copyright file="OpenDocumentRenderingTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Allors.Documents;
using Allors.Documents.OpenDocument;
using Xunit;

public class OpenDocumentRenderingTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    [Fact]
    public void BindingIsReplacedByModelValue()
    {
        var document = Odt.Document($"<text:p>Hello {Odt.Placeholder("<$Name>")}!</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", "Koen")));

        var content = Odt.Content(result);
        var paragraph = content.Descendants(Text + "p").Single();
        Assert.Equal("Hello Koen!", paragraph.Value);
        Assert.Empty(content.Descendants(Text + "placeholder"));
    }

    [Fact]
    public void BindingEscapesMarkup()
    {
        var document = Odt.Document($"<text:p>{Odt.Placeholder("<$Name>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", "<b>&\"quotes\"")));

        var content = Odt.Content(result);
        Assert.Equal("<b>&\"quotes\"", content.Descendants(Text + "p").Single().Value);

        var raw = Odt.Entry(result, Odt.ContentFileName);
        Assert.Contains("&lt;b&gt;&amp;", raw);
    }

    [Fact]
    public void BindingStripsInvalidControlCharacters()
    {
        var document = Odt.Document($"<text:p>{Odt.Placeholder("<$Name>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", "ab\tc")));

        Assert.Equal("ab\tc", Odt.Content(result).Descendants(Text + "p").Single().Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhitespaceBindingRendersEmpty(string? value)
    {
        var document = Odt.Document($"<text:p>[{Odt.Placeholder("<$Name>")}]</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", value)));

        Assert.Equal("[]", Odt.Content(result).Descendants(Text + "p").Single().Value);
    }

    [Fact]
    public void NumbersRenderWithInvariantCulture()
    {
        var document = Odt.Document($"<text:p>{Odt.Placeholder("<$Amount>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Amount", 1234.5)));

        Assert.Equal("1234.5", Odt.Content(result).Descendants(Text + "p").Single().Value);
    }

    [Fact]
    public void IfTrueKeepsContentAndRemovesMarkerParagraphs()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@if Name>")}</text:p>" +
            $"<text:p>Present: {Odt.Placeholder("<$Name>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", "Koen")));

        var paragraphs = Odt.Content(result).Descendants(Text + "p").ToList();
        var paragraph = Assert.Single(paragraphs);
        Assert.Equal("Present: Koen", paragraph.Value);
    }

    [Fact]
    public void IfFalseRemovesBlock()
    {
        var document = Odt.Document(
            "<text:p>before</text:p>" +
            $"<text:p>{Odt.Placeholder("<@if Name>")}</text:p>" +
            $"<text:p>Present: {Odt.Placeholder("<$Name>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>" +
            "<text:p>after</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", null)));

        var paragraphs = Odt.Content(result).Descendants(Text + "p").Select(p => p.Value).ToList();
        Assert.Equal(new[] { "before", "after" }, paragraphs);
    }

    [Fact]
    public void InlineIfWithinOneParagraph()
    {
        var document = Odt.Document(
            $"<text:p>Hello {Odt.Placeholder("<@if Name>")}dear {Odt.Placeholder("<$Name>")}{Odt.Placeholder("<@end>")}!</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        Assert.Equal(
            "Hello dear Koen!",
            Odt.Content(template.Render(Model(("Name", "Koen")))).Descendants(Text + "p").Single().Value);

        Assert.Equal(
            "Hello !",
            Odt.Content(template.Render(Model(("Name", null)))).Descendants(Text + "p").Single().Value);
    }

    [Fact]
    public void IfWithComparisonExpression()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@if Age >= 18>")}</text:p>" +
            "<text:p>adult</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        Assert.Single(Odt.Content(template.Render(Model(("Age", 21)))).Descendants(Text + "p"));
        Assert.Empty(Odt.Content(template.Render(Model(("Age", 12)))).Descendants(Text + "p"));
    }

    [Fact]
    public void IfComparisonPreservesInt64Precision()
    {
        // 9007199254740992 = 2^53; a double comparison cannot distinguish it from 2^53 + 1.
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@if Id == 9007199254740993>")}</text:p>" +
            "<text:p>match</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        Assert.Empty(Odt.Content(template.Render(Model(("Id", 9007199254740992L)))).Descendants(Text + "p"));
        Assert.Single(Odt.Content(template.Render(Model(("Id", 9007199254740993L)))).Descendants(Text + "p"));
    }

    [Fact]
    public void ForRepeatsBlockPerItem()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@for p People>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<$p.FirstName>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("People", new[]
        {
            new ModelPerson { FirstName = "John" },
            new ModelPerson { FirstName = "Jane" },
        })));

        var paragraphs = Odt.Content(result).Descendants(Text + "p").Select(p => p.Value).ToList();
        Assert.Equal(new[] { "John", "Jane" }, paragraphs);
    }

    [Fact]
    public void EmptyOrNullCollectionRemovesBlock()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@for p People>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<$p.FirstName>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        Assert.Empty(Odt.Content(template.Render(Model(("People", new ModelPerson[0])))).Descendants(Text + "p"));
        Assert.Empty(Odt.Content(template.Render(Model(("People", null)))).Descendants(Text + "p"));
    }

    [Fact]
    public void NestedForLoopsUseTheirOwnScopes()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@for p People>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@for p2 People>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<$p.FirstName>")}-{Odt.Placeholder("<$p2.FirstName>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("People", new[]
        {
            new ModelPerson { FirstName = "A" },
            new ModelPerson { FirstName = "B" },
        })));

        var paragraphs = Odt.Content(result).Descendants(Text + "p").Select(p => p.Value).ToList();
        Assert.Equal(new[] { "A-A", "A-B", "B-A", "B-B" }, paragraphs);
    }

    [Fact]
    public void ForProvidesIndexVariables()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@for p People>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<$i>")}/{Odt.Placeholder("<$i0>")}:{Odt.Placeholder("<$p.FirstName>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("People", new[]
        {
            new ModelPerson { FirstName = "John" },
            new ModelPerson { FirstName = "Jane" },
        })));

        var paragraphs = Odt.Content(result).Descendants(Text + "p").Select(p => p.Value).ToList();
        Assert.Equal(new[] { "1/0:John", "2/1:Jane" }, paragraphs);
    }

    [Fact]
    public void LoopVariableShadowsModelKey()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@for Name Names>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<$Name>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<$Name>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", "outer"), ("Names", new[] { "inner" })));

        var paragraphs = Odt.Content(result).Descendants(Text + "p").Select(p => p.Value).ToList();
        Assert.Equal(new[] { "inner", "outer" }, paragraphs);
    }

    [Fact]
    public void IfInsideForUsesLoopScope()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@for p People>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@if p.FirstName>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<$p.FirstName>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("People", new[]
        {
            new ModelPerson { FirstName = "John" },
            new ModelPerson(),
            new ModelPerson { FirstName = "Jane" },
        })));

        var paragraphs = Odt.Content(result).Descendants(Text + "p").Select(p => p.Value).ToList();
        Assert.Equal(new[] { "John", "Jane" }, paragraphs);
    }

    [Fact]
    public void TableRowsAreRepeated()
    {
        var document = Odt.Document(
            "<table:table>" +
            $"<table:table-row><table:table-cell><text:p>{Odt.Placeholder("<@for p People>")}</text:p></table:table-cell></table:table-row>" +
            $"<table:table-row><table:table-cell><text:p>{Odt.Placeholder("<$p.FirstName>")}</text:p></table:table-cell></table:table-row>" +
            $"<table:table-row><table:table-cell><text:p>{Odt.Placeholder("<@end>")}</text:p></table:table-cell></table:table-row>" +
            "</table:table>");
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("People", new[]
        {
            new ModelPerson { FirstName = "John" },
            new ModelPerson { FirstName = "Jane" },
        })));

        XNamespace table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        var rows = Odt.Content(result).Descendants(table + "table-row").ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(new[] { "John", "Jane" }, rows.Select(row => row.Value.Trim()));
    }

    [Fact]
    public void ImageSubstitutionRewritesHrefManifestAndContents()
    {
        var imageBytes = new byte[] { 1, 2, 3, 4 };
        var originalBytes = new byte[] { 9, 9 };
        var document = Odt.Document(
            $"""<text:p><draw:frame draw:name="$Photo"><draw:image xlink:href="Pictures/original.png"/></draw:frame></text:p>""",
            manifestXml: ManifestXml("""<manifest:file-entry manifest:full-path="Pictures/original.png" manifest:media-type="image/png"/>"""),
            files: ("Pictures/original.png", originalBytes));
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(
            Model(("Photo", "photo")),
            new Dictionary<string, byte[]> { ["photo"] = imageBytes });

        var content = Odt.Content(result);
        var frame = content.Descendants(Draw + "frame").Single();
        Assert.Equal("photo", frame.Attribute(Draw + "name")!.Value);
        Assert.Equal("Pictures/photo", frame.Element(Draw + "image")!.Attribute(Xlink + "href")!.Value);

        var manifest = XDocument.Parse(Odt.Entry(result, Odt.ManifestFileName));
        var paths = manifest.Descendants(Manifest + "file-entry")
            .Select(entry => entry.Attribute(Manifest + "full-path")!.Value)
            .ToList();
        Assert.Contains("Pictures/original.png", paths);
        Assert.Contains("Pictures/photo", paths);

        var entries = Odt.Unzip(result);
        Assert.Equal(imageBytes, entries["Pictures/photo"]);
        Assert.Equal(originalBytes, entries["Pictures/original.png"]);
    }

    [Fact]
    public void ImageInLoopResolvesNamePerIteration()
    {
        var document = Odt.Document(
            $"<text:p>{Odt.Placeholder("<@for image Images>")}</text:p>" +
            $"""<text:p><draw:frame draw:name="$image"><draw:image xlink:href="Pictures/original.png"/></draw:frame></text:p>""" +
            $"<text:p>{Odt.Placeholder("<@end>")}</text:p>",
            manifestXml: ManifestXml("""<manifest:file-entry manifest:full-path="Pictures/original.png" manifest:media-type="image/png"/>"""),
            files: ("Pictures/original.png", new byte[] { 9 }));
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(
            Model(("Images", new[] { "number1", "number2" })),
            new Dictionary<string, byte[]> { ["number1"] = new byte[] { 1 }, ["number2"] = new byte[] { 2 } });

        var content = Odt.Content(result);
        var names = content.Descendants(Draw + "frame").Select(frame => frame.Attribute(Draw + "name")!.Value).ToList();
        Assert.Equal(new[] { "number1", "number2" }, names);

        var hrefs = content.Descendants(Draw + "image").Select(image => image.Attribute(Xlink + "href")!.Value).ToList();
        Assert.Equal(new[] { "Pictures/number1", "Pictures/number2" }, hrefs);

        var entries = Odt.Unzip(result);
        Assert.Equal(new byte[] { 1 }, entries["Pictures/number1"]);
        Assert.Equal(new byte[] { 2 }, entries["Pictures/number2"]);

        var manifest = XDocument.Parse(Odt.Entry(result, Odt.ManifestFileName));
        var added = manifest.Descendants(Manifest + "file-entry")
            .Count(entry => entry.Attribute(Manifest + "full-path")!.Value.StartsWith("Pictures/number"));
        Assert.Equal(2, added);
    }

    [Fact]
    public void StaticFrameMatchingImageNameIsSubstituted()
    {
        var document = Odt.Document(
            $"""<text:p><draw:frame draw:name="logo"><draw:image xlink:href="Pictures/original.png"/></draw:frame></text:p>""",
            manifestXml: ManifestXml("""<manifest:file-entry manifest:full-path="Pictures/original.png" manifest:media-type="image/png"/>"""),
            files: ("Pictures/original.png", new byte[] { 9 }));
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(), new Dictionary<string, byte[]> { ["logo"] = new byte[] { 7 } });

        var frame = Odt.Content(result).Descendants(Draw + "frame").Single();
        Assert.Equal("Pictures/logo", frame.Element(Draw + "image")!.Attribute(Xlink + "href")!.Value);
        Assert.Equal(new byte[] { 7 }, Odt.Unzip(result)["Pictures/logo"]);
    }

    [Fact]
    public void UnreferencedImagesAreNotAddedToThePackage()
    {
        var document = Odt.Document(
            "<text:p>no frames here</text:p>",
            manifestXml: ManifestXml(string.Empty));
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(), new Dictionary<string, byte[]> { ["unused"] = new byte[] { 1 } });

        Assert.DoesNotContain("Pictures/unused", Odt.Unzip(result).Keys);
    }

    [Fact]
    public void ImagesWithoutManifestThrow()
    {
        var document = Odt.Document("<text:p>hello</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        var exception = Assert.Throws<TemplateException>(() =>
            template.Render(Model(), new Dictionary<string, byte[]> { ["x"] = new byte[] { 1 } }));

        Assert.Contains("manifest", exception.Message);
    }

    [Fact]
    public void StylesAreRendered()
    {
        var stylesXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <office:document-styles
                xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
            <office:master-styles><style-header><text:p>Header: {Odt.Placeholder("<$Name>")}</text:p></style-header></office:master-styles>
            </office:document-styles>
            """;
        var document = Odt.Document("<text:p>body</text:p>", stylesXml: stylesXml);
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model(("Name", "Koen")));

        var styles = XDocument.Parse(Odt.Entry(result, Odt.StylesFileName));
        Assert.Equal("Header: Koen", styles.Descendants(Text + "p").Single().Value);
    }

    [Fact]
    public void OtherPackageEntriesAreCopiedVerbatim()
    {
        var settings = Encoding.UTF8.GetBytes("<settings/>");
        var document = Odt.Document("<text:p>x</text:p>", files: ("settings.xml", settings));
        var template = OpenDocumentTemplate.Load(document);

        var result = template.Render(Model());

        Assert.Equal(settings, Odt.Unzip(result)["settings.xml"]);
    }

    [Fact]
    public void LoadValidatesTagsEagerly()
    {
        var document = Odt.Document($"<text:p>{Odt.Placeholder("<@for p People>")}</text:p>");

        var exception = Assert.Throws<TemplateException>(() => OpenDocumentTemplate.Load(document));

        var error = Assert.Single(exception.Errors);
        Assert.Equal(Odt.ContentFileName, error.Source);
        Assert.Contains("Missing '<@end>'", error.Message);
    }

    [Fact]
    public void LoadRejectsNonZipDocuments()
    {
        var exception = Assert.Throws<TemplateException>(() => OpenDocumentTemplate.Load(new byte[] { 1, 2, 3 }));

        Assert.Contains("not a valid OpenDocument file", exception.Message);
    }

    [Fact]
    public void TemplateIsReusableAcrossRenders()
    {
        var document = Odt.Document($"<text:p>{Odt.Placeholder("<$Name>")}</text:p>");
        var template = OpenDocumentTemplate.Load(document);

        Assert.Equal("first", Odt.Content(template.Render(Model(("Name", "first")))).Descendants(Text + "p").Single().Value);
        Assert.Equal("second", Odt.Content(template.Render(Model(("Name", "second")))).Descendants(Text + "p").Single().Value);
    }

    [Fact]
    public void TypedTemplateRendersModelMembers()
    {
        var document = Odt.Document($"<text:p>{Odt.Placeholder("<$Person.FirstName>")}</text:p>");
        var template = OpenDocumentTemplate<Model>.Load(document);

        var result = template.Render(new Model { Person = new ModelPerson { FirstName = "Jane" } });

        Assert.Equal("Jane", Odt.Content(result).Descendants(Text + "p").Single().Value);
    }

    private static Dictionary<string, object?> Model(params (string Key, object? Value)[] entries)
    {
        var model = new Dictionary<string, object?>();
        foreach (var (key, value) in entries)
        {
            model[key] = value;
        }

        return model;
    }

    private static string ManifestXml(string entries) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <manifest:manifest xmlns:manifest="urn:oasis:names:tc:opendocument:xmlns:manifest:1.0" manifest:version="1.2">
        <manifest:file-entry manifest:full-path="/" manifest:version="1.2" manifest:media-type="application/vnd.oasis.opendocument.text"/>
        {entries}
        </manifest:manifest>
        """;
}
