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
    /// Renders every bound value, so that a value never breaks the XML it lands in and reads as typed.
    /// Register it for <see cref="object"/>: StringTemplate picks the renderer by assignability.
    /// </summary>
    public class XmlStringRenderer : IAttributeRenderer
    {
        /// <summary>A value in paragraph text: escaped, with its white space as typed.</summary>
        public const string XmlStringFormat = "xml-string";

        /// <summary>A value in an attribute: escaped, without the characters an attribute cannot hold.</summary>
        public const string XmlAttributeFormat = "xml-attribute";

        private static readonly StringRenderer DefaultRenderer = new StringRenderer();

        public string ToString(object obj, string formatString, CultureInfo culture)
        {
            if (obj is string text)
            {
                switch (formatString)
                {
                    case XmlStringFormat:
                        return XmlString(text);
                    case XmlAttributeFormat:
                        return XmlAttribute(text);
                    default:
                        return DefaultRenderer.ToString(text, formatString, culture);
                }
            }

            var plain = PlainString(obj);
            switch (formatString)
            {
                case XmlStringFormat:
                    return XmlString(plain);
                case XmlAttributeFormat:
                    return XmlAttribute(plain);
                default:
                    return plain;
            }
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

        /// <summary>
        /// Escapes the value for paragraph text and writes its white space the way Writer saves typed text:
        /// a tab as text:tab, a line break as text:line-break and every space after the first of a run as text:s.
        /// Other characters below U+0020 are dropped.
        /// </summary>
        public static string XmlString(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var stringBuilder = new StringBuilder(text.Length);
            var spaces = 0;

            for (var i = 0; i < text.Length; i++)
            {
                var chr = text[i];

                if (chr == ' ')
                {
                    spaces++;
                    continue;
                }

                AppendSpaces(stringBuilder, spaces);
                spaces = 0;

                switch (chr)
                {
                    case '<':
                        stringBuilder.Append("&lt;");
                        break;
                    case '>':
                        stringBuilder.Append("&gt;");
                        break;
                    case '&':
                        stringBuilder.Append("&amp;");
                        break;
                    case '\t':
                        stringBuilder.Append("<text:tab/>");
                        break;
                    case '\n':
                        stringBuilder.Append("<text:line-break/>");
                        break;
                    case '\r':
                        stringBuilder.Append("<text:line-break/>");
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                        {
                            i++;
                        }

                        break;
                    default:
                        if (chr >= ' ')
                        {
                            stringBuilder.Append(chr);
                        }

                        break;
                }
            }

            AppendSpaces(stringBuilder, spaces);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Escapes the value for an attribute. Characters below U+0020 are dropped: an attribute cannot hold them.
        /// </summary>
        public static string XmlAttribute(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var stringBuilder = new StringBuilder(text.Length);

            foreach (var chr in text)
            {
                switch (chr)
                {
                    case '<':
                        stringBuilder.Append("&lt;");
                        break;
                    case '>':
                        stringBuilder.Append("&gt;");
                        break;
                    case '&':
                        stringBuilder.Append("&amp;");
                        break;
                    case '"':
                        stringBuilder.Append("&quot;");
                        break;
                    default:
                        if (chr >= ' ')
                        {
                            stringBuilder.Append(chr);
                        }

                        break;
                }
            }

            return stringBuilder.ToString();
        }

        private static void AppendSpaces(StringBuilder stringBuilder, int count)
        {
            if (count == 0)
            {
                return;
            }

            stringBuilder.Append(' ');

            if (count == 2)
            {
                stringBuilder.Append("<text:s/>");
            }
            else if (count > 2)
            {
                stringBuilder.Append("<text:s text:c=\"").Append((count - 1).ToString(CultureInfo.InvariantCulture)).Append("\"/>");
            }
        }
    }
}
