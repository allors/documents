// <copyright file="Finding.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>One thing wrong or doubtful in a template, in the author's terms, with where it sits.</summary>
    public sealed class Finding
    {
        internal Finding(FindingKind kind, FindingSeverity severity, string text, string path, string message, Location location)
        {
            this.Kind = kind;
            this.Severity = severity;
            this.Text = text;
            this.Path = path;
            this.Message = message;
            this.Location = location;
        }

        public FindingKind Kind { get; }

        public FindingSeverity Severity { get; }

        /// <summary>The tag, frame name or setting as the author typed it.</summary>
        public string Text { get; }

        /// <summary>The path that was checked, when the finding is about one; null otherwise.</summary>
        public string Path { get; }

        public string Message { get; }

        public Location Location { get; }

        public override string ToString() => this.Severity.ToString().ToLowerInvariant() + ": " + this.Text + ": " + this.Message + " (" + this.Location + ")";
    }
}
