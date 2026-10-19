// <copyright file="InspectedPicture.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Xml;

    /// <summary>A picture frame bound to the model: its description is a picture tag, <c>&lt;$path empty="keep" fit="contain"&gt;</c>, and the frame shows the picture at that path.</summary>
    public sealed class InspectedPicture
    {
        internal InspectedPicture(string frameName, Tag tag, Location location, InspectedTag? block, InspectedTag? branch, XmlElement element)
        {
            this.FrameName = frameName;
            this.Tag = tag;
            this.Location = location;
            this.Block = block;
            this.Branch = branch;
            this.Element = element;
        }

        /// <summary>The frame element, for the program that renders it.</summary>
        internal XmlElement Element { get; }

        /// <summary>The block tag that opened the branch the frame sits in: the loop or condition itself, or the else of a condition; null at the top level.</summary>
        internal InspectedTag? Branch { get; }

        /// <summary>The name of the frame, as the author gave it.</summary>
        public string FrameName { get; }

        /// <summary>The picture tag from the frame's description: its path and attributes.</summary>
        public Tag Tag { get; }

        /// <summary>The path of the picture to show. A picture tag always has one.</summary>
        public string Path => this.Tag.Path!;

        public Location Location { get; }

        /// <summary>The loop or condition the frame sits in; null at the top level.</summary>
        public InspectedTag? Block { get; }

        public override string ToString() => this.Tag.Text + " in " + this.Location;
    }
}
