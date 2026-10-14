// <copyright file="InspectedTag.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Inspection
{
    using System.Collections.Generic;
    using Allors.Document.Tags;

    /// <summary>A tag and where it sits: the part, the paragraph's text and the paragraph's ordinal in that part.</summary>
    public sealed class InspectedTag
    {
        private readonly List<InspectedTag> children = new List<InspectedTag>();

        internal InspectedTag(Tag tag, Location location, InspectedTag block)
        {
            this.Tag = tag;
            this.Location = location;
            this.Block = block;
        }

        public Tag Tag { get; }

        public Location Location { get; }

        /// <summary>The loop or condition this tag sits in; null at the top level.</summary>
        public InspectedTag Block { get; }

        /// <summary>The tags inside this loop or condition, in document order; empty for a binding or an end tag.</summary>
        public IReadOnlyList<InspectedTag> Children => this.children;

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

        internal void Add(InspectedTag child) => this.children.Add(child);

        public override string ToString() => this.Tag.Text + " in " + this.Location;
    }
}
