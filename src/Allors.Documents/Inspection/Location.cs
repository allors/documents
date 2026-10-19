// <copyright file="Location.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>Where something sits in a template: the part (content.xml or styles.xml), the text of its paragraph and that paragraph's ordinal in the part. A location is a value: two locations with the same members are equal.</summary>
    /// <param name="Part">The part: content.xml or styles.xml.</param>
    /// <param name="Paragraph">The 1-based ordinal of the paragraph or heading in the part; 0 when the element is outside any paragraph.</param>
    /// <param name="ParagraphText">The paragraph's text with the tags as the author typed them, white space collapsed, at most 200 characters.</param>
    public sealed record Location(string Part, int Paragraph, string ParagraphText)
    {
        public override string ToString() => this.Paragraph == 0 ? this.Part : this.Part + " paragraph " + this.Paragraph + " \"" + this.ParagraphText + "\"";
    }
}
