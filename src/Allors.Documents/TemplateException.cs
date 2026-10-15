// <copyright file="TemplateException.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document
{
    using System;
    using System.Collections.Generic;
    using Allors.Document.Validation;

#pragma warning disable RCS1194 // Implement exception constructors.

    /// <summary>
    /// A template that cannot render: a block never ends, an end opens nothing, or a block begins and ends where it would cut a table.
    /// Thrown when the template is read; <see cref="Findings"/> says what is wrong, with the tag and its paragraph.
    /// </summary>
    public class TemplateException : Exception
    {
        internal TemplateException(IReadOnlyList<Finding> findings)
            : base(string.Join("\n", findings)) =>
            this.Findings = findings;

        /// <summary>What is wrong with the template's blocks, in document order; never empty.</summary>
        public IReadOnlyList<Finding> Findings { get; }
    }
}
