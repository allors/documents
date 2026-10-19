// <copyright file="Fitting.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;

    /// <summary>
    /// The size a bound frame takes for its picture under the fit its tag asks. Fill keeps the frame as the author drew it; contain shrinks the side
    /// that would overflow, so that the picture keeps its proportions inside the frame; width and height keep that side and let the other follow the
    /// picture's proportions, which may grow the frame. A side that changes is written in its own unit; a side that stays keeps its text.
    /// </summary>
    internal static class Fitting
    {
        /// <summary>
        /// The frame's new width and height as OpenDocument lengths, null for a side that stays. False when the frame's width or height cannot be read
        /// as a length, in which case the frame stays as it is.
        /// </summary>
        public static bool TryFit(PictureFit fit, string width, string height, double pictureWidth, double pictureHeight, out string? fittedWidth, out string? fittedHeight)
        {
            fittedWidth = null;
            fittedHeight = null;
            if (fit == PictureFit.Fill)
            {
                return true;
            }

            if (!Length.TryParse(width, null, out var w, out var widthUnit) || !Length.TryParse(height, null, out var h, out var heightUnit) || w <= 0 || h <= 0)
            {
                return false;
            }

            // Both as width per unit of height: the picture's and the frame's
            var ratio = pictureWidth / pictureHeight;
            var box = Length.Convert(w, widthUnit, heightUnit) / h;
            if (fit == PictureFit.Contain)
            {
                if (Math.Abs(box - ratio) <= ratio * 1e-6)
                {
                    return true;
                }

                // A frame wider than the picture keeps its height and loses width; a taller one keeps its width and loses height
                fit = box > ratio ? PictureFit.Height : PictureFit.Width;
            }

            if (fit == PictureFit.Width)
            {
                fittedHeight = Length.Format(Length.Convert(w, widthUnit, heightUnit) / ratio, heightUnit);
            }
            else
            {
                fittedWidth = Length.Format(Length.Convert(h, heightUnit, widthUnit) * ratio, widthUnit);
            }

            return true;
        }
    }
}
