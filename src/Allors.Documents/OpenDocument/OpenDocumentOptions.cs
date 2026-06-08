// <copyright file="OpenDocumentOptions.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.OpenDocument;

/// <summary>Options for loading an <see cref="OpenDocumentTemplate"/>.</summary>
public sealed class OpenDocumentOptions
{
    internal static readonly OpenDocumentOptions Default = new();

    /// <summary>
    /// Whether members of model objects may be resolved through reflection when no generated
    /// accessor is registered. Defaults to <see langword="true"/>.
    /// </summary>
    public bool UseReflectionFallback { get; init; } = true;
}
