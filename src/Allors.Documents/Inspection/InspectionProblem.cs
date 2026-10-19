// <copyright file="InspectionProblem.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>Something in a template that cannot be read as the author meant it: a malformed tag, an end without a block or a block without an end.</summary>
    public sealed class InspectionProblem
    {
        internal InspectionProblem(string text, string message, Location location, bool isUnbalanced = false)
        {
            this.Text = text;
            this.Message = message;
            this.Location = location;
            this.IsUnbalanced = isUnbalanced;
        }

        /// <summary>True for a block without an end or an end without a block, false for a tag that cannot be read.</summary>
        public bool IsUnbalanced { get; }

        /// <summary>The tag or frame name as the author typed it.</summary>
        public string Text { get; }

        public string Message { get; }

        public Location Location { get; }

        public override string ToString() => this.Text + ": " + this.Message + " (" + this.Location + ")";
    }
}
