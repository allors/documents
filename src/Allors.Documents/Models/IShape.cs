// <copyright file="IShape.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// The static view of a model: which members a value has and what a loop over it yields, without any data.
    /// Validation walks a template's paths over shapes. An open shape accepts every member and can be iterated.
    /// </summary>
    public interface IShape
    {
        /// <summary>A name for findings, such as the type name.</summary>
        string Name { get; }

        /// <summary>The members this shape declares; empty for an open shape.</summary>
        IEnumerable<string> Members { get; }

        /// <summary>The shape of the member with this name, or null when the member is unknown.</summary>
        IShape Member(string name);

        /// <summary>True when a loop over a value of this shape yields its items.</summary>
        bool IsCollection { get; }

        /// <summary>The shape of one item of the collection; null unless <see cref="IsCollection"/>.</summary>
        IShape Element { get; }
    }
}
