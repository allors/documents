// <copyright file="Finding.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>One thing wrong or doubtful in a template, in the author's terms, with where it sits. A finding is a value: two findings with the same members are equal.</summary>
    /// <param name="Kind">What is wrong.</param>
    /// <param name="Severity">Whether the document misses a value, an error, or is complete but not as the template asked, a warning.</param>
    /// <param name="Text">The tag, frame name or setting as the author typed it.</param>
    /// <param name="Path">The path that was checked, when the finding is about one; null otherwise.</param>
    /// <param name="Message">What is wrong and what to use instead, in the author's terms.</param>
    /// <param name="Location">Where the text sits in the template.</param>
    public sealed record Finding(FindingKind Kind, FindingSeverity Severity, string Text, string? Path, string Message, Location Location)
    {
        public override string ToString() => this.Severity.ToString().ToLowerInvariant() + ": " + this.Text + ": " + this.Message + " (" + this.Location + ")";
    }
}
