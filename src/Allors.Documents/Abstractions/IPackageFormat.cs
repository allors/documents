// <copyright file="IPackageFormat.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>A document package format (e.g. OpenDocument) that can load templates from a stream.</summary>
public interface IPackageFormat
{
    /// <summary>Determines whether this format can open a document starting with the given header bytes.</summary>
    /// <param name="header">The first bytes of the document.</param>
    /// <returns><see langword="true"/> when the document looks like this format.</returns>
    bool CanOpen(ReadOnlySpan<byte> header);

    /// <summary>Loads a template from the given stream.</summary>
    /// <param name="source">The stream containing the template document.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loaded template.</returns>
    Task<IDocumentTemplate> LoadAsync(Stream source, CancellationToken cancellationToken = default);
}
