// <copyright file="SortKey.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Tags
{
    /// <summary>One key of a loop's sort attribute: a path relative to the item, ascending unless marked desc.</summary>
    public sealed class SortKey
    {
        internal SortKey(string path, bool descending)
        {
            this.Path = path;
            this.Descending = descending;
        }

        public string Path { get; }

        public bool Descending { get; }

        public override string ToString() => this.Descending ? this.Path + " desc" : this.Path;
    }
}
