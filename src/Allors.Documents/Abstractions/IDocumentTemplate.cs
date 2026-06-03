// <copyright file="IDocumentTemplate.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>A loaded document template that can be rendered against a model.</summary>
public interface IDocumentTemplate
{
    /// <summary>Renders the template against the given model.</summary>
    /// <param name="model">The model whose top level keys are resolvable from template expressions.</param>
    /// <param name="images">Optional images, keyed by the name used in the template.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The rendered document.</returns>
    Task<byte[]> RenderAsync(IReadOnlyDictionary<string, object?> model, IReadOnlyDictionary<string, byte[]>? images = null, CancellationToken cancellationToken = default);

    /// <summary>Renders the template against the given model.</summary>
    /// <param name="model">The model whose top level keys are resolvable from template expressions.</param>
    /// <param name="images">Optional images, keyed by the name used in the template.</param>
    /// <returns>The rendered document.</returns>
    byte[] Render(IReadOnlyDictionary<string, object?> model, IReadOnlyDictionary<string, byte[]>? images = null);
}
