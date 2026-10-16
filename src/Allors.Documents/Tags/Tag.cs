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

        /// <summary>The picture at a path, in the description of a picture frame: &lt;$path empty="remove|keep" fit="fill|contain|width|height"&gt;.</summary>
        Picture,

        /// <summary>A loop over the collection at a path, optionally ordered, counted and separated: &lt;@for variable path sort="key, key desc" index="n" separator=", "&gt;.</summary>
        For,

        /// <summary>A block kept when the value at a path is true, or false after not: &lt;@if path&gt;, &lt;@if not path&gt;.</summary>
        If,

        /// <summary>A block kept when the member at a path exists, whatever its value, or does not exist after not: &lt;@ifdef path&gt;, &lt;@ifdef not path&gt;.</summary>
        IfDef,

        /// <summary>The branch of a condition taken when the branches before it are not: &lt;@else&gt;, or with a test of its own, &lt;@else if path&gt; and &lt;@else ifdef path&gt;.</summary>
        Else,

        /// <summary>A map in the first cell of a two-column table: &lt;@map Name&gt;; the rows below are its keys and texts.</summary>
        Map,

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

        internal Tag(TagKind kind, string text, string path, string variable, IReadOnlyDictionary<string, string> attributes, IReadOnlyList<SortKey> sort = null, string index = null, string separator = null, bool defined = false, bool negated = false)
        {
            this.Kind = kind;
            this.Text = text;
            this.Path = path;
            this.Variable = variable;
            this.Attributes = attributes ?? NoAttributes;
            this.Sort = sort ?? NoSort;
            this.Index = index;
            this.Separator = separator;
            this.Defined = defined;
            this.Negated = negated;
        }

        public TagKind Kind { get; }

        public string Text { get; }

        /// <summary>The path of a binding, a picture, a loop or a condition, the path an else tests, or the name of a map; null for an end tag and for an else without a test.</summary>
        public string Path { get; }

        /// <summary>The loop variable of a for tag; null otherwise.</summary>
        public string Variable { get; }

        /// <summary>The attributes of a binding, a picture or a loop, by name (case-insensitive); empty otherwise.</summary>
        public IReadOnlyDictionary<string, string> Attributes { get; }

        /// <summary>The sort keys of a loop, from its sort attribute, in order; empty otherwise.</summary>
        public IReadOnlyList<SortKey> Sort { get; }

        /// <summary>The name of a loop's counter, from its index attribute: 1 for the first item in the loop's order; null when the loop declares none.</summary>
        public string Index { get; }

        /// <summary>The text a loop writes between two items, from its separator attribute; null when the loop declares none.</summary>
        public string Separator { get; }

        /// <summary>True when a condition tests whether the member at its path exists, as ifdef and else ifdef do, rather than whether its value is true.</summary>
        public bool Defined { get; }

        /// <summary>True when a condition is negated with not: the branch is taken when the test fails.</summary>
        public bool Negated { get; }

        /// <summary>True for an else with a test of its own: else if or else ifdef.</summary>
        public bool HasTest => this.Kind == TagKind.Else && this.Path != null;

        public override string ToString() => this.Text;
    }
}
