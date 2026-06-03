// <copyright file="OpenDocumentTemplate.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.OpenDocument;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Allors.Documents.Expressions;
using Allors.Documents.Templating;

/// <summary>
/// An OpenDocument (.odt) template. Loading parses and validates the template once;
/// the instance is immutable and can render any number of models, also concurrently.
/// </summary>
public class OpenDocumentTemplate : IDocumentTemplate
{
    private readonly OpenDocumentPackage package;

    private readonly ValueAccessor accessor;

    private protected OpenDocumentTemplate(OpenDocumentPackage package, OpenDocumentOptions options)
    {
        this.package = package;
        this.accessor = ValueAccessor.For(options.UseReflectionFallback);
    }

    /// <summary>Loads and validates a template from the bytes of an .odt document.</summary>
    /// <param name="document">The .odt document.</param>
    /// <param name="options">Optional load options.</param>
    /// <returns>The loaded template.</returns>
    /// <exception cref="TemplateException">The document is not a valid OpenDocument file or contains invalid template tags.</exception>
    public static OpenDocumentTemplate Load(byte[] document, OpenDocumentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var package = ReadAndValidate(document);
        return new OpenDocumentTemplate(package, options ?? OpenDocumentOptions.Default);
    }

    /// <summary>Loads and validates a template from a stream containing an .odt document.</summary>
    /// <param name="document">The stream with the .odt document.</param>
    /// <param name="options">Optional load options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded template.</returns>
    /// <exception cref="TemplateException">The document is not a valid OpenDocument file or contains invalid template tags.</exception>
    public static async Task<OpenDocumentTemplate> LoadAsync(Stream document, OpenDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Load(await ToBytesAsync(document, cancellationToken).ConfigureAwait(false), options);
    }

    /// <inheritdoc/>
    public byte[] Render(IReadOnlyDictionary<string, object?> model, IReadOnlyDictionary<string, byte[]>? images = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        return this.RenderCore(model, images);
    }

    /// <inheritdoc/>
    public Task<byte[]> RenderAsync(IReadOnlyDictionary<string, object?> model, IReadOnlyDictionary<string, byte[]>? images = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(this.Render(model, images));

    private protected static async Task<byte[]> ToBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private protected static OpenDocumentPackage ReadAndValidate(byte[] document)
    {
        var package = OpenDocumentPackage.Read(document);

        // Surface tag errors at load time rather than at first render.
        Validate(package.Content, OpenDocumentPackage.ContentFileName);

        if (package.Styles is not null)
        {
            Validate(package.Styles, OpenDocumentPackage.StylesFileName);
        }

        return package;
    }

    private protected byte[] RenderCore(object model, IReadOnlyDictionary<string, byte[]>? images)
    {
        var scope = new RenderScope(model, this.accessor);

        var content = new XDocument(this.package.Content);
        new OpenDocumentRenderer(OpenDocumentPackage.ContentFileName).Render(content, scope);

        XDocument? styles = null;
        if (this.package.Styles is not null)
        {
            styles = new XDocument(this.package.Styles);
            new OpenDocumentRenderer(OpenDocumentPackage.StylesFileName).Render(styles, scope);
        }

        var manifest = this.package.Manifest is null ? null : new XDocument(this.package.Manifest);

        var resolvedImages = images is null
            ? new List<ResolvedImage>()
            : images.Select(image => new ResolvedImage(image.Key, image.Value)).ToList();

        if (resolvedImages.Count > 0)
        {
            if (manifest is null)
            {
                throw new TemplateException($"Images were provided but the document has no {OpenDocumentPackage.ManifestFileName}.");
            }

            var documents = styles is null ? new[] { content } : new[] { content, styles };
            OpenDocumentImageProcessor.Process(documents, manifest, resolvedImages);
        }

        return OpenDocumentPackage.Write(content, styles, manifest, this.package.FileByFileName, resolvedImages);
    }

    private static void Validate(XDocument document, string fileName)
    {
        if (document.Root is null)
        {
            throw new TemplateException(new[] { new TemplateError("The document has no root element.", fileName) });
        }

        TagScanner.Scan(new XNode[] { document.Root }, fileName);
    }
}
