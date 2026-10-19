// <copyright file="TextRenderers.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// The text renderers every template has: <c>upper</c>, <c>lower</c> and <c>cap</c> transform the text the resolver formatted, in the
    /// culture of the formatting. A caller may list a renderer with one of these names in the settings to replace it.
    /// </summary>
    public static class TextRenderers
    {
        /// <summary>The text in upper case: <c>&lt;$Customer.Name format="upper"&gt;</c>.</summary>
        public static readonly ITextRenderer Upper = new Transformation("upper", (text, culture) => text.ToUpper(culture));

        /// <summary>The text in lower case: <c>&lt;$Customer.Email format="lower"&gt;</c>.</summary>
        public static readonly ITextRenderer Lower = new Transformation("lower", (text, culture) => text.ToLower(culture));

        /// <summary>The text with its first character in upper case and the rest as it is: <c>&lt;$item.Description format="cap"&gt;</c>.</summary>
        public static readonly ITextRenderer Cap = new Transformation("cap", (text, culture) => text.Length == 0 ? text : char.ToUpper(text[0], culture) + text.Substring(1));

        /// <summary>The built-in text renderers, in the order a template lists them before the caller's.</summary>
        public static readonly IReadOnlyList<ITextRenderer> All = new[] { Upper, Lower, Cap };

        /// <summary>A renderer that transforms the formatted text in the culture of the formatting; null text stays null.</summary>
        private sealed class Transformation : ITextRenderer
        {
            private readonly Func<string, CultureInfo, string> transform;

            public Transformation(string name, Func<string, CultureInfo, string> transform)
            {
                this.Name = name;
                this.transform = transform;
            }

            public string Name { get; }

            public string? Render(object value, string text, Formatting formatting) => text == null ? null : this.transform(text, (formatting ?? Formatting.Default).Culture);

            public override string ToString() => this.Name;
        }
    }
}
