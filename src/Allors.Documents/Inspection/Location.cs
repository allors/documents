// <copyright file="Location.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>Where something sits in a template: the part (content.xml or styles.xml), the text of its paragraph and that paragraph's ordinal in the part.</summary>
    public sealed class Location
    {
        internal Location(string part, int paragraph, string paragraphText)
        {
            this.Part = part;
            this.Paragraph = paragraph;
            this.ParagraphText = paragraphText;
        }

        public string Part { get; }

        /// <summary>The 1-based ordinal of the paragraph or heading in the part; 0 when the element is outside any paragraph.</summary>
        public int Paragraph { get; }

        /// <summary>The paragraph's text with the tags as the author typed them, white space collapsed, at most 200 characters.</summary>
        public string ParagraphText { get; }

        public override string ToString() => this.Paragraph == 0 ? this.Part : this.Part + " paragraph " + this.Paragraph + " \"" + this.ParagraphText + "\"";
    }
}
