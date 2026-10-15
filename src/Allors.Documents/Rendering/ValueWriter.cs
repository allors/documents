// <copyright file="ValueWriter.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Rendering
{
    using System.Globalization;
    using System.Xml;

    /// <summary>Where the pieces of a value go: literal text, the spaces of a run after its first, a tab and a line break.</summary>
    internal interface IValueSink
    {
        void Text(string text, int start, int length);

        /// <summary>The spaces of a run after its first, which went out as text; at least one.</summary>
        void Spaces(int count);

        void Tab();

        void LineBreak();
    }

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
            if (!string.IsNullOrEmpty(text))
            {
                Scan(text, new XmlWriterSink(writer));
            }
        }

        /// <summary>Cuts a value into the pieces a sink writes, in order.</summary>
        public static void Scan(string text, IValueSink sink)
        {
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

                        Flush(text, start, i + 1, sink);
                        if (end > i + 1)
                        {
                            sink.Spaces(end - i - 1);
                        }

                        i = end;
                        start = i;
                        break;

                    case '\t':
                        Flush(text, start, i, sink);
                        sink.Tab();
                        i++;
                        start = i;
                        break;

                    case '\n':
                        Flush(text, start, i, sink);
                        sink.LineBreak();
                        i++;
                        start = i;
                        break;

                    case '\r':
                        Flush(text, start, i, sink);
                        sink.LineBreak();
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
                            Flush(text, start, i, sink);
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

            Flush(text, start, text.Length, sink);
        }

        private static void Flush(string text, int start, int end, IValueSink sink)
        {
            if (end > start)
            {
                sink.Text(text, start, end - start);
            }
        }

        private sealed class XmlWriterSink : IValueSink
        {
            private readonly XmlWriter writer;

            public XmlWriterSink(XmlWriter writer) => this.writer = writer;

            public void Text(string text, int start, int length) => this.writer.WriteString(start == 0 && length == text.Length ? text : text.Substring(start, length));

            public void Spaces(int count)
            {
                this.writer.WriteStartElement("text", "s", TextNamespace);
                if (count > 1)
                {
                    this.writer.WriteAttributeString("text", "c", TextNamespace, count.ToString(CultureInfo.InvariantCulture));
                }

                this.writer.WriteEndElement();
            }

            public void Tab()
            {
                this.writer.WriteStartElement("text", "tab", TextNamespace);
                this.writer.WriteEndElement();
            }

            public void LineBreak()
            {
                this.writer.WriteStartElement("text", "line-break", TextNamespace);
                this.writer.WriteEndElement();
            }
        }
    }
}
