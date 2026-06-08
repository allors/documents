// <copyright file="OpenDocumentTemplateTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Allors.Documents.OpenDocument;
using Xunit;

/// <summary>Renders the real EmbeddedTemplate.odt fixture and verifies the generated document.</summary>
public class OpenDocumentTemplateTests
{
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

    [Fact]
    public void Render()
    {
        var template = OpenDocumentTemplate<Model>.Load(GetResource("EmbeddedTemplate.odt"));

        var result = template.Render(CreateModel(), CreateImages());

        var content = Odt.Content(result);

        // All template tags are resolved.
        Assert.Empty(content.Descendants(Text + "placeholder"));

        // The People loops repeat their blocks: the fixture contains four People constructs
        // (paragraph loop, inline loop, nested loop and a table loop).
        var text = content.Descendants(Text + "p").Select(p => p.Value.Trim()).ToList();
        Assert.Contains(text, value => value.Contains("John"));
        Assert.Contains(text, value => value.Contains("Jenny"));

        // The nested loop produces the cross product of People x People.
        Assert.Contains(text, value => value.Contains("John") && value.Contains("Jenny"));

        // The truthy <@if Person.FirstName> keeps Jane.
        Assert.Contains(text, value => value.Contains("Jane"));

        // The image loop resolves one frame per image, with retargeted hrefs.
        var frames = content.Descendants(Draw + "frame").ToList();
        var frameNames = frames.Select(frame => frame.Attribute(Draw + "name")!.Value).ToList();
        Assert.Contains("number1", frameNames);
        Assert.Contains("number2", frameNames);
        Assert.Contains("number3", frameNames);
        Assert.Contains("logo2", frameNames);

        var hrefs = content.Descendants(Draw + "image").Select(image => image.Attribute(Xlink + "href")!.Value).ToList();
        Assert.Contains("Pictures/number1", hrefs);
        Assert.Contains("Pictures/number2", hrefs);
        Assert.Contains("Pictures/number3", hrefs);
        Assert.Contains("Pictures/logo2", hrefs);

        // The styles header logo is substituted as well.
        var styles = XDocument.Parse(Odt.Entry(result, Odt.StylesFileName));
        var headerImage = styles.Descendants(Draw + "image").Single();
        Assert.Equal("Pictures/logo", headerImage.Attribute(Xlink + "href")!.Value);

        // Substituted images are in the package, with manifest entries.
        var entries = Odt.Unzip(result);
        Assert.Equal(GetResource("1.png"), entries["Pictures/number1"]);
        Assert.Equal(GetResource("2.png"), entries["Pictures/number2"]);
        Assert.Equal(GetResource("3.png"), entries["Pictures/number3"]);
        Assert.Equal(GetResource("logo.png"), entries["Pictures/logo"]);
        Assert.Equal(GetResource("logo.png"), entries["Pictures/logo2"]);

        var manifest = XDocument.Parse(Odt.Entry(result, Odt.ManifestFileName));
        var manifestPaths = manifest.Descendants(Manifest + "file-entry")
            .Select(entry => entry.Attribute(Manifest + "full-path")!.Value)
            .ToList();
        Assert.Contains("Pictures/number1", manifestPaths);
        Assert.Contains("Pictures/number2", manifestPaths);
        Assert.Contains("Pictures/number3", manifestPaths);
        Assert.Contains("Pictures/logo", manifestPaths);
        Assert.Contains("Pictures/logo2", manifestPaths);
        Assert.Equal(manifestPaths.Count, manifestPaths.Distinct().Count());
    }

    [Fact]
    public void RerenderProducesIdenticalContent()
    {
        var template = OpenDocumentTemplate<Model>.Load(GetResource("EmbeddedTemplate.odt"));
        var model = CreateModel();
        var images = CreateImages();

        var first = template.Render(model, images);
        var second = template.Render(model, images);

        Assert.Equal(Odt.Entry(first, Odt.ContentFileName), Odt.Entry(second, Odt.ContentFileName));
        Assert.Equal(Odt.Entry(first, Odt.StylesFileName), Odt.Entry(second, Odt.StylesFileName));
        Assert.Equal(Odt.Entry(first, Odt.ManifestFileName), Odt.Entry(second, Odt.ManifestFileName));
    }

    private static Model CreateModel() => new()
    {
        Person = new ModelPerson { FirstName = "Jane" },
        People =
        [
            new ModelPerson { FirstName = "John" },
            new ModelPerson { FirstName = "Jenny" }
        ],
        Images =
        [
            "number1",
            "number2",
            "number3"
        ],
    };

    private static Dictionary<string, byte[]> CreateImages() => new()
    {
        { "logo", GetResource("logo.png") },
        { "logo2", GetResource("logo.png") },
        { "number1", GetResource("1.png") },
        { "number2", GetResource("2.png") },
        { "number3", GetResource("3.png") },
    };

    private static byte[] GetResource(string name)
    {
        var assembly = typeof(OpenDocumentTemplateTests).GetTypeInfo().Assembly;

        var resourceName = assembly.GetManifestResourceNames().First(v => v.Contains(name));
        using var resource = assembly.GetManifestResourceStream(resourceName);

        using var output = new MemoryStream();
        resource?.CopyTo(output);
        return output.ToArray();
    }
}
