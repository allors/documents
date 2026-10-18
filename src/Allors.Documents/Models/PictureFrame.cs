// <copyright file="PictureFrame.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Models
{
    using System.Globalization;
    using Allors.Document.Tags;

    /// <summary>
    /// The frame a picture goes into, as the author drew it: its size in inches and the fit its tag asks, for a renderer that draws a picture
    /// to size and for a processor that scales one to it. The size is the frame's before the fit changes it.
    /// </summary>
    public sealed class PictureFrame
    {
        public PictureFrame(string path, double width, double height, PictureFit fit)
        {
            this.Path = path;
            this.Width = width;
            this.Height = height;
            this.Fit = fit;
        }

        /// <summary>The path of the picture tag, for a message.</summary>
        public string Path { get; }

        /// <summary>The frame's width in inches as the author drew it; 0 when the frame has no width, or a relative one.</summary>
        public double Width { get; }

        /// <summary>The frame's height in inches as the author drew it; 0 when the frame has no height, or a relative one.</summary>
        public double Height { get; }

        /// <summary>How the picture takes the frame, from the tag's fit attribute: fill unless the tag says otherwise.</summary>
        public PictureFit Fit { get; }

        /// <summary>True when the frame has a width and a height.</summary>
        public bool HasSize => this.Width > 0 && this.Height > 0;

        public override string ToString() =>
            this.Path + " " + (this.HasSize ? this.Width.ToString("0.####", CultureInfo.InvariantCulture) + "x" + this.Height.ToString("0.####", CultureInfo.InvariantCulture) + "in" : "without a size") + " " + this.Fit.ToString().ToLowerInvariant();
    }
}
