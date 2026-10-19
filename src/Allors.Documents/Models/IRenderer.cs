// <copyright file="IRenderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>
    /// A display transformation of one value, which a tag selects by name through its format attribute: <c>&lt;$Customer.Name format="upper"&gt;</c>
    /// on a binding, <c>&lt;$Invoice.Number format="code128"&gt;</c> on a picture frame. A renderer encodes, formats or draws the one value it is
    /// given; it never reads a second value, compares or decides, which is the model's work. The caller lists its renderers in
    /// <see cref="RenderSettings.Renderers"/>; a renderer is an <see cref="ITextRenderer"/>, an <see cref="IPictureRenderer"/> or both.
    /// A renderer is shared by every render of a template, from any thread, so it keeps no state of a render.
    /// </summary>
    public interface IRenderer
    {
        /// <summary>The name a tag selects the renderer by, in any case; unique in a template, where a later renderer replaces an earlier one with its name.</summary>
        string Name { get; }
    }
}
