// <copyright file="AccessorRegistry.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Expressions;

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Registry of <see cref="ITypedAccessor"/> instances by model type.
/// The Allors.Documents source generator registers generated accessors here at module initialization.
/// </summary>
public static class AccessorRegistry
{
    private static readonly ConcurrentDictionary<Type, ITypedAccessor> AccessorByType = new();

    /// <summary>Registers an accessor for the given model type.</summary>
    /// <param name="type">The model type.</param>
    /// <param name="accessor">The accessor that resolves members of the model type.</param>
    public static void Register(Type type, ITypedAccessor accessor) => AccessorByType[type] = accessor;

    /// <summary>Tries to find an accessor for the given type or one of its base types.</summary>
    /// <param name="type">The runtime type to find an accessor for.</param>
    /// <param name="accessor">The registered accessor when found.</param>
    /// <returns><see langword="true"/> when an accessor is registered.</returns>
    public static bool TryGet(Type type, [NotNullWhen(true)] out ITypedAccessor? accessor)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            if (AccessorByType.TryGetValue(current, out accessor))
            {
                return true;
            }
        }

        accessor = null;
        return false;
    }
}
