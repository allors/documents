// <copyright file="XmlStringRenderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Globalization;
using System.Text;
using Antlr4.StringTemplate;

namespace Allors.Document.Xml
{
    /// <summary>
    /// Renders every bound value, so that a value never breaks the XML it lands in.
    /// Register it for <see cref="object"/>: StringTemplate picks the renderer by assignability.
    /// </summary>
    public class XmlStringRenderer : IAttributeRenderer
    {
        public const string XmlStringFormat = "xml-string";

        private static readonly StringRenderer DefaultRenderer = new StringRenderer();

        public string ToString(object obj, string formatString, CultureInfo culture)
        {
            if (obj is string text)
            {
                return XmlStringFormat.Equals(formatString) ? XmlString(text) : DefaultRenderer.ToString(text, formatString, culture);
            }

            var plain = PlainString(obj);
            return XmlStringFormat.Equals(formatString) ? XmlString(plain) : plain;
        }

        /// <summary>
        /// Converts a value the way StringTemplate does without a renderer: booleans in lower case, everything else through ToString in the current culture.
        /// </summary>
        public static string PlainString(object obj)
        {
            if (obj is bool flag)
            {
                return flag ? "true" : "false";
            }

            return obj?.ToString();
        }

        public static string XmlString(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                var stringBuilder = new StringBuilder(text.Length);

                foreach (var chr in text)
                {
                    if (chr == '<')
                    {
                        stringBuilder.Append("&lt;");
                    }
                    else if (chr == '>')
                    {
                        stringBuilder.Append("&gt;");
                    }
                    else if (chr == '&')
                    {
                        stringBuilder.Append("&amp;");
                    }
                    else if (chr >= 32)
                    {
                        stringBuilder.Append(chr);
                    }
                }

                return stringBuilder.ToString();
            }

            return string.Empty;
        }
    }
}
