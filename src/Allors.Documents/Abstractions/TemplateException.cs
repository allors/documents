// <copyright file="TemplateException.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents;

using System;
using System.Collections.Generic;

/// <summary>Thrown when a template cannot be loaded or rendered.</summary>
public sealed class TemplateException : Exception
{
    public TemplateException(string message)
        : this(new[] { new TemplateError(message) })
    {
    }

    public TemplateException(IReadOnlyList<TemplateError> errors)
        : base(string.Join("\n", errors)) =>
        this.Errors = errors;

    /// <summary>The individual errors that caused this exception.</summary>
    public IReadOnlyList<TemplateError> Errors { get; }
}
