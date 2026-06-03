// <copyright file="OpenDocumentTemplate{T}.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.OpenDocument;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An OpenDocument (.odt) template rendered against a strongly typed model.
/// Members of <typeparamref name="T"/> are resolved through a generated accessor when
/// available, falling back to reflection.
/// </summary>
/// <typeparam name="T">The model type whose public members are resolvable from template expressions.</typeparam>
public sealed class OpenDocumentTemplate<T> : OpenDocumentTemplate, IDocumentTemplate<T>
{
    private OpenDocumentTemplate(OpenDocumentPackage package, OpenDocumentOptions options)
        : base(package, options)
    {
    }

    /// <summary>Loads and validates a template from the bytes of an .odt document.</summary>
    /// <param name="document">The .odt document.</param>
    /// <param name="options">Optional load options.</param>
    /// <returns>The loaded template.</returns>
    /// <exception cref="TemplateException">The document is not a valid OpenDocument file or contains invalid template tags.</exception>
    public static new OpenDocumentTemplate<T> Load(byte[] document, OpenDocumentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var package = ReadAndValidate(document);
        return new OpenDocumentTemplate<T>(package, options ?? OpenDocumentOptions.Default);
    }

    /// <summary>Loads and validates a template from a stream containing an .odt document.</summary>
    /// <param name="document">The stream with the .odt document.</param>
    /// <param name="options">Optional load options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded template.</returns>
    /// <exception cref="TemplateException">The document is not a valid OpenDocument file or contains invalid template tags.</exception>
    public static new async Task<OpenDocumentTemplate<T>> LoadAsync(Stream document, OpenDocumentOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Load(await ToBytesAsync(document, cancellationToken).ConfigureAwait(false), options);
    }

    /// <inheritdoc/>
    public byte[] Render(T model, IReadOnlyDictionary<string, byte[]>? images = null)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        return this.RenderCore(model, images);
    }

    /// <inheritdoc/>
    public Task<byte[]> RenderAsync(T model, IReadOnlyDictionary<string, byte[]>? images = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(this.Render(model, images));
}
