// <copyright file="Odt.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Org.XmlUnit.Builder;
using Xunit;

/// <summary>Helpers to build and inspect in-memory .odt documents in tests.</summary>
internal static class Odt
{
    internal const string ContentFileName = "content.xml";

    internal const string StylesFileName = "styles.xml";

    internal const string ManifestFileName = "META-INF/manifest.xml";

    internal const string DocumentContentHeader = """
        <office:document-content
            xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
            xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
            xmlns:table="urn:oasis:names:tc:opendocument:xmlns:table:1.0"
            xmlns:draw="urn:oasis:names:tc:opendocument:xmlns:drawing:1.0"
            xmlns:svg="urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0"
            xmlns:xlink="http://www.w3.org/1999/xlink">
        """;

    /// <summary>Builds a minimal .odt document with the given body content inside office:body/office:text.</summary>
    internal static byte[] Document(string body, string? stylesXml = null, string? manifestXml = null, params (string Name, byte[] Contents)[] files)
    {
        var contentXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            {DocumentContentHeader}
            <office:body><office:text>{body}</office:text></office:body>
            </office:document-content>
            """;

        using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, ContentFileName, Encoding.UTF8.GetBytes(contentXml));

            if (stylesXml is not null)
            {
                Add(archive, StylesFileName, Encoding.UTF8.GetBytes(stylesXml));
            }

            if (manifestXml is not null)
            {
                Add(archive, ManifestFileName, Encoding.UTF8.GetBytes(manifestXml));
            }

            foreach (var (name, contents) in files)
            {
                Add(archive, name, contents);
            }
        }

        return output.ToArray();
    }

    private static void Add(ZipArchive archive, string name, byte[] contents)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(contents, 0, contents.Length);
    }

    /// <summary>A placeholder element carrying a template tag; the tag text is XML-escaped automatically.</summary>
    internal static string Placeholder(string tag) =>
        $"""<text:placeholder text:placeholder-type="text">{new XText(tag)}</text:placeholder>""";

    internal static Dictionary<string, byte[]> Unzip(byte[] document)
    {
        var entries = new Dictionary<string, byte[]>(System.StringComparer.Ordinal);

        using var stream = new MemoryStream(document);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            entries[entry.FullName] = buffer.ToArray();
        }

        return entries;
    }

    internal static string Entry(byte[] document, string name)
    {
        var entries = Unzip(document);
        Assert.True(entries.ContainsKey(name), $"The document does not contain '{name}'.");
        return Encoding.UTF8.GetString(entries[name]);
    }

    internal static XDocument Content(byte[] document) => XDocument.Parse(Entry(document, ContentFileName));

    /// <summary>Asserts that two XML documents are equal, ignoring whitespace between elements.</summary>
    internal static void AssertXmlEqual(string expected, string actual)
    {
        var diff = DiffBuilder
            .Compare(Input.FromString(expected))
            .WithTest(Input.FromString(actual))
            .IgnoreWhitespace()
            .CheckForSimilar()
            .Build();

        Assert.False(diff.HasDifferences(), diff.ToString());
    }
}
