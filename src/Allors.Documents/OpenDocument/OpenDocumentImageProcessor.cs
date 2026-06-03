// <copyright file="OpenDocumentImageProcessor.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.OpenDocument;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Allors.Documents.Templating;

/// <summary>An image being substituted into a rendered document.</summary>
internal sealed class ResolvedImage
{
    internal ResolvedImage(string name, byte[] contents)
    {
        this.Name = name;
        this.Contents = contents;
    }

    internal string Name { get; }

    internal byte[] Contents { get; }

    /// <summary>The package path of the original image this one replaces; null when no frame references it.</summary>
    internal string? OriginalFullPath { get; set; }

    internal string FullPath => "Pictures/" + this.Name;
}

/// <summary>
/// Substitutes images on the rendered DOM: every <c>draw:frame</c> whose (already evaluated)
/// <c>draw:name</c> matches a provided image gets its <c>draw:image/@xlink:href</c> retargeted
/// to the new image, and the manifest receives a cloned <c>file-entry</c> per distinct new path.
/// Only images actually referenced by a frame end up in the package.
/// </summary>
internal static class OpenDocumentImageProcessor
{
    private const string ManifestNamespacePart = "opendocument:xmlns:manifest";

    private const string XlinkNamespacePart = "xlink";

    internal static void Process(IEnumerable<XDocument> documents, XDocument manifest, IReadOnlyList<ResolvedImage> images)
    {
        foreach (var document in documents)
        {
            ProcessFrames(document, images);
        }

        UpdateManifest(manifest, images);
    }

    private static void ProcessFrames(XDocument document, IReadOnlyList<ResolvedImage> images)
    {
        var frames = document.Descendants().Where(element =>
            element.Name.LocalName.Equals("frame", StringComparison.Ordinal) &&
            element.Name.NamespaceName.Contains(TagSyntax.DrawNamespacePart, StringComparison.Ordinal));

        foreach (var frame in frames)
        {
            var nameAttribute = frame.Attributes().FirstOrDefault(attribute =>
                attribute.Name.LocalName.Equals("name", StringComparison.Ordinal) &&
                attribute.Name.NamespaceName.Contains(TagSyntax.DrawNamespacePart, StringComparison.Ordinal));

            if (nameAttribute is null)
            {
                continue;
            }

            var name = nameAttribute.Value.Trim();
            var image = images.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.Ordinal));
            if (image is null)
            {
                continue;
            }

            var imageElement = frame.Descendants().FirstOrDefault(element =>
                element.Name.LocalName.Equals("image", StringComparison.Ordinal) &&
                element.Name.NamespaceName.Contains(TagSyntax.DrawNamespacePart, StringComparison.Ordinal));

            if (imageElement is null)
            {
                throw new TemplateException($"Frame '{name}' matches an image but has no draw:image element.");
            }

            var hrefAttribute = imageElement.Attributes().FirstOrDefault(attribute =>
                attribute.Name.LocalName.Equals("href", StringComparison.Ordinal) &&
                attribute.Name.NamespaceName.Contains(XlinkNamespacePart, StringComparison.Ordinal));

            if (hrefAttribute is null)
            {
                throw new TemplateException($"Frame '{name}' matches an image but its draw:image has no xlink:href attribute.");
            }

            image.OriginalFullPath ??= hrefAttribute.Value;
            hrefAttribute.Value = image.FullPath;
        }
    }

    private static void UpdateManifest(XDocument manifest, IReadOnlyList<ResolvedImage> images)
    {
        var root = manifest.Root ?? throw new TemplateException("The manifest has no root element.");

        var entries = root.Elements()
            .Where(element => element.Name.LocalName.Equals("file-entry", StringComparison.Ordinal))
            .ToList();

        var existingPaths = new HashSet<string>(
            entries.Select(entry => FullPathAttribute(entry)?.Value).OfType<string>(),
            StringComparer.Ordinal);

        foreach (var image in images.Where(image => image.OriginalFullPath is not null))
        {
            if (!existingPaths.Add(image.FullPath))
            {
                continue;
            }

            var original = entries.FirstOrDefault(entry => FullPathAttribute(entry)?.Value == image.OriginalFullPath);
            if (original is null)
            {
                continue;
            }

            var clone = new XElement(original);
            FullPathAttribute(clone)!.Value = image.FullPath;
            root.Add(clone);
        }
    }

    private static XAttribute? FullPathAttribute(XElement entry) =>
        entry.Attributes().FirstOrDefault(attribute =>
            attribute.Name.LocalName.Equals("full-path", StringComparison.Ordinal) &&
            attribute.Name.NamespaceName.Contains(ManifestNamespacePart, StringComparison.Ordinal));
}
