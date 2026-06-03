// <copyright file="TemplateError.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents;

using System.Text;

/// <summary>A single error discovered while loading or rendering a template.</summary>
public sealed class TemplateError
{
    public TemplateError(string message, string? source = null, string? tag = null)
    {
        this.Message = message;
        this.Source = source;
        this.Tag = tag;
    }

    /// <summary>The human readable error message.</summary>
    public string Message { get; }

    /// <summary>The template part the error originates from (e.g. "content.xml").</summary>
    public string? Source { get; }

    /// <summary>The offending tag text, when known.</summary>
    public string? Tag { get; }

    public override string ToString()
    {
        var builder = new StringBuilder();

        if (this.Source is not null)
        {
            builder.Append(this.Source).Append(": ");
        }

        builder.Append(this.Message);

        if (this.Tag is not null)
        {
            builder.Append(" (tag: ").Append(this.Tag).Append(')');
        }

        return builder.ToString();
    }
}
