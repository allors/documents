// <copyright file="NarrowedShape.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The variants of an abstract shape that have a guarded member: inside the guard, a member counts when every one of them has it.
    /// </summary>
    internal sealed class NarrowedShape : IShape
    {
        private readonly IReadOnlyList<IShape> variants;

        internal NarrowedShape(IReadOnlyList<IShape> variants) => this.variants = variants;

        public string Name => string.Join(" or ", this.variants.Select(v => v.Name));

        /// <summary>The kind the variants share; an object when they differ.</summary>
        public ValueKind Kind => this.variants.All(v => v.Kind == this.variants[0].Kind) ? this.variants[0].Kind : ValueKind.Object;

        public IEnumerable<string> Members => this.variants.Skip(1).Aggregate((IEnumerable<string>)this.variants[0].Members, (common, variant) => common.Intersect(variant.Members));

        public IShape Element => this.Kind == ValueKind.Collection || this.Kind == ValueKind.Open ? this.variants[0].Element : null;

        public IEnumerable<IShape> Variants => this.variants;

        public IShape Member(string name)
        {
            var first = this.variants[0].Member(name);
            return first != null && this.variants.Skip(1).All(v => v.Member(name) != null) ? first : null;
        }

        public override string ToString() => this.Name;
    }
}
