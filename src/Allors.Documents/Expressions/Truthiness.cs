// <copyright file="Truthiness.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Expressions;

using System;
using System.Collections;

/// <summary>
/// The truthiness rules used by conditional tags:
/// null is false, booleans are themselves, strings are true when non-empty,
/// numbers are true when non-zero, enumerables are true when they have at least one element,
/// and any other non-null value is true.
/// </summary>
internal static class Truthiness
{
    internal static bool IsTruthy(object? value) =>
        value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            sbyte n => n != 0,
            byte n => n != 0,
            short n => n != 0,
            ushort n => n != 0,
            int n => n != 0,
            uint n => n != 0,
            long n => n != 0,
            ulong n => n != 0,
            float n => n != 0,
            double n => n != 0,
            decimal n => n != 0,
            IEnumerable enumerable => HasAny(enumerable),
            _ => true,
        };

    private static bool HasAny(IEnumerable enumerable)
    {
        var enumerator = enumerable.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }
}
