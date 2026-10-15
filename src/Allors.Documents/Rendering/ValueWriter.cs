// <copyright file="ValueWriter.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Rendering
{
    using System.Globalization;
    using System.Xml;

    /// <summary>
    /// Writes a value the way Writer saves typed text, so that the document shows it as typed: a tab as text:tab, a line break as
    /// text:line-break and every space after the first of a run as text:s. Characters XML cannot hold are dropped: the other characters
    /// below U+0020, lone surrogates and the two non-characters U+FFFE and U+FFFF.
    /// </summary>
    internal static class ValueWriter
    {
        private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        public static void Write(XmlWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var start = 0;
            var i = 0;
            while (i < text.Length)
            {
                var chr = text[i];
                switch (chr)
                {
                    case ' ':
                        var end = i + 1;
                        while (end < text.Length && text[end] == ' ')
                        {
                            end++;
                        }

                        // The first space of a run goes out as text; the rest as one text:s
                        Flush(writer, text, start, i + 1);
                        if (end > i + 1)
                        {
                            Spaces(writer, end - i - 1);
                        }

                        i = end;
                        start = i;
                        break;

                    case '\t':
                        Flush(writer, text, start, i);
                        Element(writer, "tab");
                        i++;
                        start = i;
                        break;

                    case '\n':
                        Flush(writer, text, start, i);
                        Element(writer, "line-break");
                        i++;
                        start = i;
                        break;

                    case '\r':
                        Flush(writer, text, start, i);
                        Element(writer, "line-break");
                        i += i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                        start = i;
                        break;

                    default:
                        if (char.IsHighSurrogate(chr) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                        {
                            i += 2;
                        }
                        else if (chr < ' ' || char.IsSurrogate(chr) || chr == '￾' || chr == '￿')
                        {
                            Flush(writer, text, start, i);
                            i++;
                            start = i;
                        }
                        else
                        {
                            i++;
                        }

                        break;
                }
            }

            Flush(writer, text, start, text.Length);
        }

        private static void Flush(XmlWriter writer, string text, int start, int end)
        {
            if (end > start)
            {
                writer.WriteString(start == 0 && end == text.Length ? text : text.Substring(start, end - start));
            }
        }

        private static void Spaces(XmlWriter writer, int count)
        {
            writer.WriteStartElement("text", "s", TextNamespace);
            if (count > 1)
            {
                writer.WriteAttributeString("text", "c", TextNamespace, count.ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteEndElement();
        }

        private static void Element(XmlWriter writer, string localName)
        {
            writer.WriteStartElement("text", localName, TextNamespace);
            writer.WriteEndElement();
        }
    }
}
