// <copyright file="Tag.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Tags
{
    using System.Collections.Generic;

    public enum TagKind
    {
        /// <summary>A value at a path, with optional attributes: &lt;$path attr="value"&gt;.</summary>
        Binding,

        /// <summary>A loop over the collection at a path, optionally ordered: &lt;@for variable path sort="key, key desc"&gt;.</summary>
        For,

        /// <summary>A block kept when the value at a path is true: &lt;@if path&gt;.</summary>
        If,

        /// <summary>The end of a loop or a condition: &lt;@end&gt;.</summary>
        End,
    }

    /// <summary>
    /// A parsed tag. The text is the tag as the author typed it, trimmed.
    /// </summary>
    public sealed class Tag
    {
        private static readonly IReadOnlyDictionary<string, string> NoAttributes = new Dictionary<string, string>();

        private static readonly IReadOnlyList<SortKey> NoSort = new SortKey[0];

        internal Tag(TagKind kind, string text, string path, string variable, IReadOnlyDictionary<string, string> attributes, IReadOnlyList<SortKey> sort = null)
        {
            this.Kind = kind;
            this.Text = text;
            this.Path = path;
            this.Variable = variable;
            this.Attributes = attributes ?? NoAttributes;
            this.Sort = sort ?? NoSort;
        }

        public TagKind Kind { get; }

        public string Text { get; }

        /// <summary>The path of a binding, a loop or a condition; null for an end tag.</summary>
        public string Path { get; }

        /// <summary>The loop variable of a for tag; null otherwise.</summary>
        public string Variable { get; }

        /// <summary>The attributes of a binding or a loop, by name (case-insensitive); empty otherwise.</summary>
        public IReadOnlyDictionary<string, string> Attributes { get; }

        /// <summary>The sort keys of a loop, from its sort attribute, in order; empty otherwise.</summary>
        public IReadOnlyList<SortKey> Sort { get; }

        public override string ToString() => this.Text;
    }
}
