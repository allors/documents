// <copyright file="IResolver.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The runtime view of a model: how the renderer reads a member, iterates a loop, tests a condition, formats a value,
    /// orders a loop and gets a picture. The host implements it over its own objects, usually by deriving from <see cref="Resolver"/>.
    /// A resolver may return its own value types from <see cref="TryGetMember"/> and recognise them in the other members.
    /// </summary>
    public interface IResolver
    {
        /// <summary>
        /// Reads the member with this name from a scope. Returns false when the scope has no such member, which the renderer reports;
        /// true with a null value when the member exists and is empty, which prints blank. The scope is never null.
        /// </summary>
        bool TryGetMember(object scope, string name, out object? value);

        /// <summary>The items a loop over this value visits, in order; empty for null.</summary>
        IEnumerable<object> Items(object? value);

        /// <summary>Whether a condition on this value keeps its block.</summary>
        bool IsTrue(object? value);

        /// <summary>The text a binding prints for this value under the effective formatting; blank for null.</summary>
        string Format(object? value, Formatting formatting);

        /// <summary>
        /// Orders two sort key values: negative when x sorts first, zero when equal, positive when y sorts first.
        /// Throws <see cref="ArgumentException"/> when the values cannot be ordered; the renderer then leaves the loop unsorted and reports it.
        /// </summary>
        int Compare(object? x, object? y, Formatting formatting);

        /// <summary>The bytes of the picture this value stands for, or null when it is not a picture. The value is never null.</summary>
        byte[]? Picture(object value);
    }
}
