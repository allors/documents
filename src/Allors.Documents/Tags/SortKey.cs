// <copyright file="SortKey.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>One key of a loop's sort attribute: a path relative to the item, ascending unless marked desc. A key is a value: two keys with the same path and direction are equal.</summary>
    /// <param name="Path">The path relative to the item.</param>
    /// <param name="Descending">True when the key is marked desc.</param>
    public sealed record SortKey(string Path, bool Descending)
    {
        public override string ToString() => this.Descending ? this.Path + " desc" : this.Path;
    }
}
