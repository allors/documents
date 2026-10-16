// <copyright file="InspectedTag.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Inspection
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Allors.Document.Tags;

    /// <summary>A tag and where it sits: the part, the paragraph's text and the paragraph's ordinal in that part.</summary>
    public sealed class InspectedTag
    {
        private readonly List<InspectedTag> children = new List<InspectedTag>();

        internal InspectedTag(Tag tag, Location location, InspectedTag block, XmlElement element)
        {
            this.Tag = tag;
            this.Location = location;
            this.Block = block;
            this.Element = element;
        }

        /// <summary>The placeholder element that holds the tag, for validation's structural checks.</summary>
        internal XmlElement Element { get; }

        public Tag Tag { get; }

        public Location Location { get; }

        /// <summary>The loop or condition this tag sits in; null at the top level. An else sits in its condition.</summary>
        public InspectedTag Block { get; }

        /// <summary>The end tag of this loop or condition; null for a binding, an end tag or a block that never ends.</summary>
        public InspectedTag End
        {
            get
            {
                var last = this.children.Count > 0 ? this.children[this.children.Count - 1] : null;
                return last?.Tag.Kind == TagKind.End ? last : null;
            }
        }

        /// <summary>The tags inside this loop or condition, in document order, the else tags and the end tag among them; empty for any other tag.</summary>
        public IReadOnlyList<InspectedTag> Children => this.children;

        /// <summary>The else tags of this condition, in document order; empty for any other tag.</summary>
        public IEnumerable<InspectedTag> Elses => this.children.Where(v => v.Tag.Kind == TagKind.Else);

        /// <summary>The loop variables in scope at this tag, innermost last.</summary>
        public IEnumerable<string> Variables
        {
            get
            {
                var variables = new List<string>();
                for (var block = this.Block; block != null; block = block.Block)
                {
                    if (block.Tag.Kind == TagKind.For)
                    {
                        variables.Insert(0, block.Tag.Variable);
                    }
                }

                return variables;
            }
        }

        /// <summary>The else tag that opened the branch the next child goes into, or null while the children go into the first branch.</summary>
        internal InspectedTag LastElse => this.children.LastOrDefault(v => v.Tag.Kind == TagKind.Else);

        internal void Add(InspectedTag child) => this.children.Add(child);

        public override string ToString() => this.Tag.Text + " in " + this.Location;
    }
}
