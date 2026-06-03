// <copyright file="OpenDocumentPackage.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.OpenDocument;

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// The parsed parts of an OpenDocument zip: content.xml, styles.xml, META-INF/manifest.xml
/// and the remaining entries verbatim. The parsed documents are the immutable template;
/// renders work on clones.
/// </summary>
internal sealed class OpenDocumentPackage
{
    internal const string ContentFileName = "content.xml";

    internal const string StylesFileName = "styles.xml";

    internal const string ManifestFileName = "META-INF/manifest.xml";

    private OpenDocumentPackage(XDocument content, XDocument? styles, XDocument? manifest, Dictionary<string, byte[]> fileByFileName)
    {
        this.Content = content;
        this.Styles = styles;
        this.Manifest = manifest;
        this.FileByFileName = fileByFileName;
    }

    internal XDocument Content { get; }

    internal XDocument? Styles { get; }

    internal XDocument? Manifest { get; }

    internal IReadOnlyDictionary<string, byte[]> FileByFileName { get; }

    internal static OpenDocumentPackage Read(byte[] document)
    {
        XDocument? content = null;
        XDocument? styles = null;
        XDocument? manifest = null;
        var fileByFileName = new Dictionary<string, byte[]>(System.StringComparer.Ordinal);

        try
        {
            using var stream = new MemoryStream(document);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                using var entryStream = entry.Open();

                switch (entry.FullName)
                {
                    case ContentFileName:
                        content = LoadXml(entryStream, ContentFileName);
                        break;

                    case StylesFileName:
                        styles = LoadXml(entryStream, StylesFileName);
                        break;

                    case ManifestFileName:
                        manifest = LoadXml(entryStream, ManifestFileName);
                        break;

                    default:
                        using (var buffer = new MemoryStream())
                        {
                            entryStream.CopyTo(buffer);
                            fileByFileName[entry.FullName] = buffer.ToArray();
                        }

                        break;
                }
            }
        }
        catch (InvalidDataException exception)
        {
            throw new TemplateException($"The document is not a valid OpenDocument file: {exception.Message}");
        }

        if (content is null)
        {
            throw new TemplateException($"The document does not contain {ContentFileName}.");
        }

        return new OpenDocumentPackage(content, styles, manifest, fileByFileName);
    }

    internal static byte[] Write(
        XDocument content,
        XDocument? styles,
        XDocument? manifest,
        IReadOnlyDictionary<string, byte[]> fileByFileName,
        IReadOnlyList<ResolvedImage> images)
    {
        var referencedImages = images.Where(image => image.OriginalFullPath is not null).ToList();
        var imagePaths = new HashSet<string>(referencedImages.Select(image => image.FullPath), System.StringComparer.Ordinal);

        using var output = new MemoryStream();

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXml(archive, ContentFileName, content);

            if (styles is not null)
            {
                WriteXml(archive, StylesFileName, styles);
            }

            if (manifest is not null)
            {
                WriteXml(archive, ManifestFileName, manifest);
            }

            foreach (var (fileName, file) in fileByFileName)
            {
                // A substituted image with the same path replaces the original entry.
                if (imagePaths.Contains(fileName))
                {
                    continue;
                }

                WriteBytes(archive, fileName, file);
            }

            foreach (var image in referencedImages)
            {
                WriteBytes(archive, image.FullPath, image.Contents);
            }
        }

        return output.ToArray();
    }

    private static XDocument LoadXml(Stream stream, string fileName)
    {
        try
        {
            return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new TemplateException(new[]
            {
                new TemplateError($"Invalid XML: {exception.Message}", fileName),
            });
        }
    }

    private static readonly XmlWriterSettings XmlWriterSettings = new()
    {
        // No byte order mark and no added indentation, like the original document parts.
        Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    private static void WriteXml(ZipArchive archive, string fileName, XDocument document)
    {
        var entry = archive.CreateEntry(fileName);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, XmlWriterSettings);
        document.Save(writer);
    }

    private static void WriteBytes(ZipArchive archive, string fileName, byte[] contents)
    {
        var entry = archive.CreateEntry(fileName);
        using var stream = entry.Open();
        stream.Write(contents, 0, contents.Length);
    }
}
