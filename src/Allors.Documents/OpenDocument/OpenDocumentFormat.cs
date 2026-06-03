// <copyright file="OpenDocumentFormat.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.OpenDocument;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>The OpenDocument (.odt) package format.</summary>
public sealed class OpenDocumentFormat : IPackageFormat
{
    /// <summary>The singleton instance.</summary>
    public static readonly OpenDocumentFormat Instance = new();

    private OpenDocumentFormat()
    {
    }

    /// <inheritdoc/>
    public bool CanOpen(ReadOnlySpan<byte> header) =>
        header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;

    /// <inheritdoc/>
    public async Task<IDocumentTemplate> LoadAsync(Stream source, CancellationToken cancellationToken = default) =>
        await OpenDocumentTemplate.LoadAsync(source, null, cancellationToken).ConfigureAwait(false);
}
