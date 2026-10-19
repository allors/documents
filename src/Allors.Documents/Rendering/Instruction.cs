// <copyright file="Instruction.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>A tag in a program: what it says and where it sits, for the findings a render reports.</summary>
    internal sealed class Instruction
    {
        public Instruction(Tag tag, Location location, bool inline = false)
        {
            this.Tag = tag;
            this.Location = location;
            this.Inline = inline;
        }

        public Tag Tag { get; }

        /// <summary>The tag's path. Every instruction the renderer resolves has one, a binding, a picture, a loop or the test of a condition; an else without a test is a branch and is never resolved.</summary>
        public string Path => this.Tag.Path!;

        public Location Location { get; }

        /// <summary>True for a block that sits inside one paragraph: text may go between its items.</summary>
        public bool Inline { get; }

        public override string ToString() => this.Tag.Text + " in " + this.Location;
    }
}
