// <copyright file="InspectedMap.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Xml;

    /// <summary>
    /// A map of a template: a table of two columns whose first cell holds <c>&lt;@map Name&gt;</c>; every row below it maps the text in its first
    /// cell, the key, to the text in its second. The row whose key is <c>default</c> serves every value the map has no row for. A binding prints
    /// through a map with <c>&lt;$path map="Name"&gt;</c>. The table leaves the output.
    /// </summary>
    public sealed class InspectedMap
    {
        internal InspectedMap(Tag tag, IReadOnlyDictionary<string, string> entries, string? defaultText, bool hasDefault, Location location, XmlElement element)
        {
            this.Tag = tag;
            this.Entries = entries;
            this.Default = defaultText;
            this.HasDefault = hasDefault;
            this.Location = location;
            this.Element = element;
        }

        /// <summary>The table element, for the program that removes it.</summary>
        internal XmlElement Element { get; }

        /// <summary>The map tag in the first cell of the table.</summary>
        public Tag Tag { get; }

        /// <summary>The name of the map, as the author gave it; case-sensitive, like any name. A map tag always has one: it is the tag's path.</summary>
        public string Name => this.Tag.Path!;

        /// <summary>The texts by key, in the order of the rows, the default row left out; keys as the author typed them.</summary>
        public IReadOnlyDictionary<string, string> Entries { get; }

        /// <summary>True when the map has a default row.</summary>
        public bool HasDefault { get; }

        /// <summary>The text of the default row; null when the map has none.</summary>
        public string? Default { get; }

        public Location Location { get; }

        /// <summary>The text for a key: the text of its row, or the default; false when the map has neither.</summary>
        public bool TryGet(string? key, [NotNullWhen(true)] out string? text)
        {
            if (key != null && this.Entries.TryGetValue(key, out text))
            {
                return true;
            }

            if (this.HasDefault && this.Default != null)
            {
                text = this.Default;
                return true;
            }

            text = null;
            return false;
        }

        public override string ToString() => this.Tag.Text + " in " + this.Location;
    }
}
