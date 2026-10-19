// <copyright file="PictureFit.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>How a picture takes the frame it is bound to, from the fit attribute of its tag: <c>&lt;$Issuer.Logo fit="contain"&gt;</c>.</summary>
    public enum PictureFit
    {
        /// <summary>The frame stays as the author drew it and the picture is stretched to it; the default, and what a barcode needs.</summary>
        Fill,

        /// <summary>The picture keeps its proportions and is as large as fits inside the frame: the side that would overflow shrinks.</summary>
        Contain,

        /// <summary>The frame keeps its width; its height follows the picture's proportions, and may grow.</summary>
        Width,

        /// <summary>The frame keeps its height; its width follows the picture's proportions, and may grow.</summary>
        Height,
    }
}
