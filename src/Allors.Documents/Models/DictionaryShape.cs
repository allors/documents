// <copyright file="DictionaryShape.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Models
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The shape of a dictionary model. Open, it accepts every member and every member is open again; closed over declared members,
    /// it knows exactly which names exist, which is how a composed root (Invoice, Issuer, ...) is validated.
    /// </summary>
    public sealed class DictionaryShape : IShape
    {
        /// <summary>The open shape: anything goes.</summary>
        public static readonly DictionaryShape Open = new DictionaryShape(null);

        private readonly IReadOnlyDictionary<string, IShape> members;

        /// <summary>A closed shape over these members; null for an open shape.</summary>
        public DictionaryShape(IReadOnlyDictionary<string, IShape> members) => this.members = members;

        public string Name => "dictionary";

        public bool IsOpen => this.members == null;

        public IEnumerable<string> Members => this.members?.Keys ?? Enumerable.Empty<string>();

        /// <summary>Open: an open shape can stand for anything, a collection included. Closed: a dictionary is not looped over.</summary>
        public bool IsCollection => this.IsOpen;

        public IShape Element => this.IsOpen ? this : null;

        public IShape Member(string name)
        {
            if (this.IsOpen)
            {
                return this;
            }

            return name != null && this.members.TryGetValue(name, out var member) ? member : null;
        }

        public override string ToString() => this.IsOpen ? "open dictionary" : "dictionary {" + string.Join(", ", this.members.Keys) + "}";
    }
}
