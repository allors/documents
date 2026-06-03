// <copyright file="Image.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents;

/// <summary>An image that can be substituted into a rendered document.</summary>
public sealed class Image
{
    /// <summary>The name that identifies the image, matched against template frame names.</summary>
    public required string Name { get; init; }

    /// <summary>The binary contents of the image.</summary>
    public required byte[] Contents { get; init; }
}
