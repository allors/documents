// <copyright file="PagedResult.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

/// <summary>An unannotated generic base; the generator does not emit an accessor for it directly.</summary>
public class PagedResult<T>
{
    public T[]? Items { get; set; }

    public int Total { get; set; }
}
