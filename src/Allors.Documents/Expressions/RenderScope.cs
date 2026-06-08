// <copyright file="RenderScope.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Expressions;

using System;
using System.Collections.Generic;

/// <summary>
/// A chain of name scopes. The root scope resolves names against the model;
/// child scopes hold loop variables that shadow outer names.
/// </summary>
internal sealed class RenderScope
{
    private readonly RenderScope? parent;

    private readonly object? model;

    private Dictionary<string, object?>? locals;

    internal RenderScope(object? model, ValueAccessor accessor)
    {
        this.model = model;
        this.Accessor = accessor;
    }

    private RenderScope(RenderScope parent)
    {
        this.parent = parent;
        this.Accessor = parent.Accessor;
    }

    internal ValueAccessor Accessor { get; }

    internal RenderScope CreateChild() => new(this);

    internal void Set(string name, object? value) =>
        (this.locals ??= new Dictionary<string, object?>(StringComparer.Ordinal))[name] = value;

    internal bool TryResolve(string name, out object? value)
    {
        for (var scope = this; scope is not null; scope = scope.parent)
        {
            if (scope.locals is not null && scope.locals.TryGetValue(name, out value))
            {
                return true;
            }

            if (scope.parent is null)
            {
                return scope.Accessor.TryGetMember(scope.model, name, out value);
            }
        }

        value = null;
        return false;
    }
}
