// <copyright file="GeneratedAccessorTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

using System.IO;
using System.Linq;
using System.Reflection;
using Allors.Documents.Expressions;
using Allors.Documents.OpenDocument;
using Xunit;

/// <summary>Verifies the source generated accessors and their parity with the reflection fallback.</summary>
public class GeneratedAccessorTests
{
    [Fact]
    public void AccessorIsGeneratedForAnnotatedModel()
    {
        Assert.True(AccessorRegistry.TryGet(typeof(Model), out var accessor));

        var model = new Model { Person = new ModelPerson { FirstName = "Jane" } };
        Assert.True(accessor!.TryGet(model, "Person", out var person));
        Assert.Same(model.Person, person);

        Assert.False(accessor.TryGet(model, "Missing", out _));
    }

    [Fact]
    public void AccessorIsGeneratedForTransitivelyReferencedModelTypes()
    {
        // ModelPerson is not annotated; it is reached through Model's members.
        Assert.True(AccessorRegistry.TryGet(typeof(ModelPerson), out var accessor));

        var person = new ModelPerson { FirstName = "John" };
        Assert.True(accessor!.TryGet(person, "FirstName", out var firstName));
        Assert.Equal("John", firstName);
    }

    [Fact]
    public void AccessorResolvesKeywordNamedMembers()
    {
        Assert.True(AccessorRegistry.TryGet(typeof(KeywordModel), out var accessor));

        var model = new KeywordModel { @class = "x", @event = 7 };

        // Lookups use the raw member name, as templates and the reflection fallback do.
        Assert.True(accessor!.TryGet(model, "class", out var classValue));
        Assert.Equal("x", classValue);

        Assert.True(accessor.TryGet(model, "event", out var eventValue));
        Assert.Equal(7, eventValue);
    }

    [Fact]
    public void AccessorIsGeneratedForKeywordNamedType()
    {
        // @struct is not annotated; it is reached through KeywordModel's members.
        Assert.True(AccessorRegistry.TryGet(typeof(@struct), out var accessor));

        Assert.True(accessor!.TryGet(new @struct { @string = "s" }, "string", out var value));
        Assert.Equal("s", value);
    }

    [Fact]
    public void GeneratedPathEqualsReflectionPath()
    {
        var document = GetResource("EmbeddedTemplate.odt");
        var model = new Model
        {
            Person = new ModelPerson { FirstName = "Jane" },
            People =
            [
                new ModelPerson { FirstName = "John" },
                new ModelPerson { FirstName = "Jenny" }
            ],
            Images = ["number1"],
        };

        var withReflection = OpenDocumentTemplate<Model>.Load(document, new OpenDocumentOptions { UseReflectionFallback = true });
        var withoutReflection = OpenDocumentTemplate<Model>.Load(document, new OpenDocumentOptions { UseReflectionFallback = false });

        var first = withReflection.Render(model);
        var second = withoutReflection.Render(model);

        Assert.Equal(Odt.Entry(first, Odt.ContentFileName), Odt.Entry(second, Odt.ContentFileName));
        Assert.Equal(Odt.Entry(first, Odt.StylesFileName), Odt.Entry(second, Odt.StylesFileName));
    }

    [Fact]
    public void WithoutReflectionFallbackUnregisteredTypesResolveToNull()
    {
        var document = Odt.Document($"<text:p>[{Odt.Placeholder("<$Person.FirstName>")}]</text:p>");
        var template = OpenDocumentTemplate.Load(document, new OpenDocumentOptions { UseReflectionFallback = false });

        var model = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["Person"] = new Unregistered { FirstName = "hidden" },
        };

        var result = template.Render(model);

        Assert.Equal("[]", Odt.Content(result).Descendants("{urn:oasis:names:tc:opendocument:xmlns:text:1.0}p").Single().Value);
    }

    [Fact]
    public void AccessorIsGeneratedForNonGenericSubclassOfClosedGeneric()
    {
        // The documented escape hatch for generics: derive a non-generic type from the closed
        // generic and annotate it; inherited members resolve without reflection.
        Assert.True(AccessorRegistry.TryGet(typeof(TextPage), out var accessor));

        var page = new TextPage { Items = new[] { "a", "b" }, Total = 2 };

        Assert.True(accessor!.TryGet(page, "Total", out var total));
        Assert.Equal(2, total);

        Assert.True(accessor.TryGet(page, "Items", out var items));
        Assert.Same(page.Items, items);
    }

    private static byte[] GetResource(string name)
    {
        var assembly = typeof(GeneratedAccessorTests).GetTypeInfo().Assembly;

        var resourceName = assembly.GetManifestResourceNames().First(v => v.Contains(name));
        using var resource = assembly.GetManifestResourceStream(resourceName);

        using var output = new MemoryStream();
        resource?.CopyTo(output);
        return output.ToArray();
    }

    private sealed class Unregistered
    {
        public string? FirstName { get; set; }
    }
}
