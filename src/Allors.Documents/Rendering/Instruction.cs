// <copyright file="Instruction.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Rendering
{
    using Allors.Document.Inspection;
    using Allors.Document.Tags;

    /// <summary>A tag in a program: what it says and where it sits, for the findings a render reports.</summary>
    internal sealed class Instruction
    {
        public Instruction(Tag tag, Location location)
        {
            this.Tag = tag;
            this.Location = location;
        }

        public Tag Tag { get; }

        public Location Location { get; }

        public override string ToString() => this.Tag.Text + " in " + this.Location;
    }
}
