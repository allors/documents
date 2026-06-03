// <copyright file="ValueAccessor.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Expressions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Resolves a member of an instance, trying in order: a generated <see cref="ITypedAccessor"/>,
/// a string keyed dictionary, and finally cached reflection (when enabled).
/// </summary>
internal sealed class ValueAccessor
{
    internal static readonly ValueAccessor Default = new(useReflectionFallback: true);

    internal static readonly ValueAccessor WithoutReflection = new(useReflectionFallback: false);

    private static readonly ConcurrentDictionary<(Type Type, string Name), Func<object, object?>?> ReflectionCache = new();

    private readonly bool useReflectionFallback;

    private ValueAccessor(bool useReflectionFallback) => this.useReflectionFallback = useReflectionFallback;

    internal static ValueAccessor For(bool useReflectionFallback) => useReflectionFallback ? Default : WithoutReflection;

    internal bool TryGetMember(object? instance, string name, out object? value)
    {
        value = null;

        if (instance is null)
        {
            return false;
        }

        if (AccessorRegistry.TryGet(instance.GetType(), out var accessor) && accessor.TryGet(instance, name, out value))
        {
            return true;
        }

        if (instance is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.TryGetValue(name, out value);
        }

        if (instance is IDictionary<string, object?> dictionary)
        {
            return dictionary.TryGetValue(name, out value);
        }

        if (this.useReflectionFallback)
        {
            var getter = ReflectionCache.GetOrAdd((instance.GetType(), name), static key => CreateGetter(key.Type, key.Name));
            if (getter is not null)
            {
                value = getter(instance);
                return true;
            }
        }

        return false;
    }

    private static Func<object, object?>? CreateGetter(Type type, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property?.GetMethod is not null)
        {
            return property.GetValue;
        }

        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (field is not null)
        {
            return field.GetValue;
        }

        return null;
    }
}
