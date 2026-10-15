// <copyright file="Fodt.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    /// <summary>Builds flat OpenDocument texts (.fodt) in code, laid out as LibreOffice writes them, and reads rendered ones back.</summary>
    internal static class Fodt
    {
        public static readonly XNamespace Meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
        public static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";

        /// <summary>A paragraph style for the body, one for the header and the page layout of the master page.</summary>
        public const string DefaultAutomaticStyles =
            "  <style:style style:name=\"P1\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"/>\n" +
            "  <style:style style:name=\"MP1\" style:family=\"paragraph\" style:parent-style-name=\"Header\"/>\n" +
            "  <style:page-layout style:name=\"pm1\">\n   <style:page-layout-properties fo:page-width=\"21cm\" fo:page-height=\"29.7cm\"/>\n  </style:page-layout>\n";

        private const string Namespaces =
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
            "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            "xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" " +
            "xmlns:config=\"urn:oasis:names:tc:opendocument:xmlns:config:1.0\" " +
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\"";

        /// <summary>A body paragraph of typed text and tags, indented as Writer writes one; a piece that starts with &lt;$ or &lt;@ becomes a placeholder.</summary>
        public static string P(params string[] pieces) => Paragraph("P1", pieces);

        /// <summary>A header paragraph.</summary>
        public static string H(params string[] pieces) => Paragraph("MP1", pieces);

        public static string Paragraph(string style, params string[] pieces) =>
            "   <text:p text:style-name=\"" + style + "\">" + string.Concat(pieces.Select(v => v.StartsWith("<$") || v.StartsWith("<@") ? Odt.Placeholder(v) : v)) + "</text:p>";

        /// <summary>
        /// A frame embedding a picture as Writer writes one in a flat document: no link, the base64 in indented lines of 72 characters and
        /// the end tags on lines of their own. A description that is a tag binds the picture to the model.
        /// </summary>
        public static string Frame(string name, byte[] picture, string description = null, string mimeType = "image/png") =>
            "<draw:frame draw:name=\"" + name + "\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"1cm\"><draw:image" + (mimeType == null ? string.Empty : " draw:mime-type=\"" + mimeType + "\"") + ">\n" +
            "      <office:binary-data>" + Base64Lines(picture) + "      </office:binary-data>\n     </draw:image>" +
            (description == null ? string.Empty : "<svg:desc>" + new XText(description) + "</svg:desc>") +
            "</draw:frame>";

        /// <summary>
        /// A flat document with the given body paragraphs, optional header paragraphs in the master page, automatic styles, custom document
        /// properties and media type (none when null), indented as LibreOffice writes one: between elements, never inside a paragraph.
        /// </summary>
        public static byte[] Build(string body, string header = null, string automaticStyles = DefaultAutomaticStyles, IDictionary<string, string> settings = null, string mimetype = Odt.MediaType)
        {
            var properties = new StringBuilder();
            foreach (var setting in settings ?? new Dictionary<string, string>())
            {
                properties.Append("<meta:user-defined meta:name=\"" + setting.Key + "\">" + new XText(setting.Value) + "</meta:user-defined>");
            }

            var text = new StringBuilder()
                .Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n\n")
                .Append("<office:document " + Namespaces + " office:version=\"1.3\"" + (mimetype == null ? string.Empty : " office:mimetype=\"" + mimetype + "\"") + ">\n")
                .Append(" <office:meta><meta:generator>Allors.Documents.Tests</meta:generator>" + properties + "</office:meta>\n")
                .Append(" <office:settings>\n  <config:config-item-set config:name=\"ooo:view-settings\">\n   <config:config-item config:name=\"ViewAreaTop\" config:type=\"long\">0</config:config-item>\n  </config:config-item-set>\n </office:settings>\n")
                .Append(" <office:scripts/>\n")
                .Append(" <office:font-face-decls>\n  <style:font-face style:name=\"Liberation Serif\" svg:font-family=\"'Liberation Serif'\"/>\n </office:font-face-decls>\n")
                .Append(" <office:styles>\n  <style:default-style style:family=\"paragraph\"/>\n  <style:style style:name=\"Standard\" style:family=\"paragraph\"/>\n  <style:style style:name=\"Header\" style:family=\"paragraph\" style:parent-style-name=\"Standard\"/>\n </office:styles>\n")
                .Append(" <office:automatic-styles>\n" + automaticStyles + " </office:automatic-styles>\n")
                .Append(" <office:master-styles>\n  <style:master-page style:name=\"Standard\" style:page-layout-name=\"pm1\">" + (header == null ? string.Empty : "\n   <style:header>\n" + header + "\n   </style:header>\n  ") + "</style:master-page>\n </office:master-styles>\n")
                .Append(" <office:body>\n  <office:text>\n" + body + "\n  </office:text>\n </office:body>\n")
                .Append("</office:document>\n");

            return new UTF8Encoding(false).GetBytes(text.ToString());
        }

        /// <summary>The flat document parsed with its white space kept; parsing throws when the render produced malformed XML.</summary>
        public static XDocument Document(byte[] flat)
        {
            using var reader = new StreamReader(new MemoryStream(flat), Encoding.UTF8, true);
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }

        /// <summary>The local names of the root's sections, in document order.</summary>
        public static IReadOnlyList<string> Sections(XDocument document) => document.Root.Elements().Select(v => v.Name.LocalName).ToList();

        public static XElement Section(XDocument document, string name) => document.Root.Element(Odt.Office + name);

        /// <summary>The frames in document order: the name and the bytes of the embedded picture, null for a frame that embeds none.</summary>
        public static IReadOnlyList<(string Name, byte[] Picture)> Frames(XContainer container) =>
            container.Descendants(Odt.Draw + "frame").Select(v => ((string)v.Attribute(Odt.Draw + "name"), Picture(v))).ToList();

        /// <summary>The names of the automatic styles of a part or a flat document, in order.</summary>
        public static IReadOnlyList<string> AutomaticStyles(XContainer container) =>
            container.Descendants(Odt.Office + "automatic-styles").Single().Elements().Select(v => (string)v.Attribute(Style + "name")).ToList();

        private static byte[] Picture(XElement frame)
        {
            var data = frame.Element(Odt.Draw + "image")?.Element(Odt.Office + "binary-data");
            return data == null ? null : Convert.FromBase64String(data.Value);
        }

        private static string Base64Lines(byte[] bytes)
        {
            var text = Convert.ToBase64String(bytes);
            var lines = new StringBuilder();
            for (var at = 0; at < text.Length; at += 72)
            {
                lines.Append(at == 0 ? string.Empty : "       ").Append(text, at, Math.Min(72, text.Length - at)).Append('\n');
            }

            return lines.ToString();
        }
    }
}
