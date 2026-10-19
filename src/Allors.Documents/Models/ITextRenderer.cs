// <copyright file="ITextRenderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>A renderer a binding may name: it gives the text the binding prints for its value.</summary>
    public interface ITextRenderer : IRenderer
    {
        /// <summary>
        /// The text a binding prints for a value: the value as the resolver read it, and the text the resolver formatted it to under the effective
        /// formatting, whose <see cref="Formatting.Format"/> is null here because the renderer's name took its place. The renderer may transform
        /// the text or render the value anew. It is never called for a null value. An exception is reported as RendererFailed and the binding prints blank.
        /// </summary>
        string Render(object value, string text, Formatting formatting);
    }
}
