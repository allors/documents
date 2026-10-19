// <copyright file="PictureFrame.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Globalization;

    /// <summary>
    /// The frame a picture goes into, as the author drew it: its size in inches and the fit its tag asks, for a renderer that draws a picture
    /// to size and for a processor that scales one to it. The size is the frame's before the fit changes it. A frame is a value: two with the same members are equal.
    /// </summary>
    /// <param name="Path">The path of the picture tag, for a message.</param>
    /// <param name="Width">The frame's width in inches as the author drew it; 0 when the frame has no width, or a relative one.</param>
    /// <param name="Height">The frame's height in inches as the author drew it; 0 when the frame has no height, or a relative one.</param>
    /// <param name="Fit">How the picture takes the frame, from the tag's fit attribute: fill unless the tag says otherwise.</param>
    public sealed record PictureFrame(string Path, double Width, double Height, PictureFit Fit)
    {
        /// <summary>True when the frame has a width and a height.</summary>
        public bool HasSize => this.Width > 0 && this.Height > 0;

        public override string ToString() =>
            this.Path + " " + (this.HasSize ? this.Width.ToString("0.####", CultureInfo.InvariantCulture) + "x" + this.Height.ToString("0.####", CultureInfo.InvariantCulture) + "in" : "without a size") + " " + this.Fit.ToString().ToLowerInvariant();
    }
}
