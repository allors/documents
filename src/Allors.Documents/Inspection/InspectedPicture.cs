// <copyright file="InspectedPicture.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Inspection
{
    /// <summary>A picture frame bound to the model: its name starts with $ and the rest is the path of the picture to show.</summary>
    public sealed class InspectedPicture
    {
        internal InspectedPicture(string frameName, string path, Location location, InspectedTag block)
        {
            this.FrameName = frameName;
            this.Path = path;
            this.Location = location;
            this.Block = block;
        }

        public string FrameName { get; }

        public string Path { get; }

        public Location Location { get; }

        /// <summary>The loop or condition the frame sits in; null at the top level.</summary>
        public InspectedTag Block { get; }

        public override string ToString() => this.FrameName + " in " + this.Location;
    }
}
