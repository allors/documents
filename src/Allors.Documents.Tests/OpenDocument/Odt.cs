// <copyright file="Odt.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using Allors.Documents;

    /// <summary>
    /// Builds minimal OpenDocument text packages in code, and reads rendered packages back.
    /// </summary>
    internal static class Odt
    {
        public const string MediaType = "application/vnd.oasis.opendocument.text";

        public static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        public static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        public static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        public static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        public static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
        public static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
        public static readonly XNamespace ManifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

        private const string Namespaces =
            "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
            "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
            "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
            "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" " +
            "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
            "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" " +
            "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
            "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\"";

        /// <summary>A text placeholder carrying a template tag, as LibreOffice writes one (Insert, Field, More Fields, Functions, Placeholder).</summary>
        public static string Placeholder(string tag) => "<text:placeholder text:placeholder-type=\"text\">" + new XText(tag).ToString() + "</text:placeholder>";

        /// <summary>
        /// A frame showing the picture at <paramref name="href"/>, as Writer saves one: the image first, then the alternative text (svg:title) and
        /// the description (svg:desc) from the image's Options. A description that is a tag binds the picture to the model. The frame is 2cm by 1cm
        /// unless the size says otherwise.
        /// </summary>
        public static string Frame(string name, string href, string description = null, string title = null, string mimeType = null, string size = "svg:width=\"2cm\" svg:height=\"1cm\"") =>
            "<draw:frame draw:name=\"" + name + "\" text:anchor-type=\"paragraph\" " + size + ">" +
            "<draw:image xlink:href=\"" + href + "\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\"" + (mimeType == null ? string.Empty : " draw:mime-type=\"" + mimeType + "\"") + "/>" +
            (title == null ? string.Empty : "<svg:title>" + new XText(title) + "</svg:title>") +
            (description == null ? string.Empty : "<svg:desc>" + new XText(description) + "</svg:desc>") +
            "</draw:frame>";

        /// <summary>
        /// A map table as an author lays one out: a first row whose first cell holds the map tag and whose second cell is a heading, then one
        /// row per entry given as "key|text", each cell a paragraph.
        /// </summary>
        public static string Map(string name, params string[] rows) =>
            "<table:table table:name=\"" + name + "\"><table:table-column table:number-columns-repeated=\"2\"/>" +
            "<table:table-row><table:table-cell><text:p>" + Placeholder("<@map " + name + ">") + "</text:p></table:table-cell><table:table-cell><text:p>Text</text:p></table:table-cell></table:table-row>" +
            string.Concat(rows.Select(row => "<table:table-row>" + string.Concat(row.Split('|').Select(cell => "<table:table-cell><text:p>" + new XText(cell) + "</text:p></table:table-cell>")) + "</table:table-row>")) +
            "</table:table>";

        /// <summary>A frame bound to the model by the picture tag in its description, showing the template's placeholder picture until it renders.</summary>
        public static string Picture(string tag, string name = "Image1", string href = "Pictures/placeholder.png", string title = null) => Frame(name, href, tag, title, "image/png");

        /// <summary>Renders a package for a dictionary model, an empty one when null, and gives the document back.</summary>
        public static byte[] Render(byte[] package, IDictionary<string, object> model = null, RenderSettings settings = null) =>
            new OpenDocumentTemplate(package, settings).Render(model ?? new Dictionary<string, object>()).Document;

        /// <summary>The manifest's entries: the media type by full path.</summary>
        public static IReadOnlyDictionary<string, string> ManifestEntries(byte[] package) =>
            Manifest(package).Descendants(ManifestNs + "file-entry").ToDictionary(v => (string)v.Attribute(ManifestNs + "full-path"), v => (string)v.Attribute(ManifestNs + "media-type"));

        /// <summary>The full paths of the manifest's picture entries, in manifest order.</summary>
        public static IReadOnlyList<string> ManifestPictures(byte[] package) =>
            Manifest(package).Descendants(ManifestNs + "file-entry").Select(v => (string)v.Attribute(ManifestNs + "full-path")).Where(v => v.StartsWith("Pictures/")).ToList();

        /// <summary>The names of the package's picture files, in package order.</summary>
        public static IReadOnlyList<string> PictureFiles(byte[] package) => Entries(package).Select(v => v.Name).Where(v => v.StartsWith("Pictures/")).ToList();

        /// <summary>The frames of a part in document order: the name and the href of the picture each shows, null for a frame without one.</summary>
        public static IReadOnlyList<(string Name, string Href)> Frames(XDocument part) =>
            part.Descendants(Draw + "frame").Select(v => ((string)v.Attribute(Draw + "name"), (string)v.Element(Draw + "image")?.Attribute(XLink + "href"))).ToList();

        /// <summary>
        /// A package with the given office:text body, optional pictures (added under Pictures/ and to the manifest) and optional master styles.
        /// </summary>
        public static byte[] Build(string body, IDictionary<string, byte[]> pictures = null, string masterStyles = null, bool withMimetype = true, IDictionary<string, string> settings = null)
        {
            var content =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<office:document-content " + Namespaces + " office:version=\"1.3\">" +
                "<office:automatic-styles><style:style style:name=\"T1\" style:family=\"text\"><style:text-properties fo:font-weight=\"bold\"/></style:style></office:automatic-styles>" +
                "<office:body><office:text>" + body + "</office:text></office:body></office:document-content>";

            var styles =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<office:document-styles " + Namespaces + " office:version=\"1.3\"><office:styles/>" + (masterStyles ?? string.Empty) + "</office:document-styles>";

            var manifest = new StringBuilder()
                .Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>")
                .Append("<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.3\">")
                .Append("<manifest:file-entry manifest:full-path=\"/\" manifest:version=\"1.3\" manifest:media-type=\"" + MediaType + "\"/>")
                .Append("<manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/>")
                .Append("<manifest:file-entry manifest:full-path=\"styles.xml\" manifest:media-type=\"text/xml\"/>");

            foreach (var picture in pictures ?? new Dictionary<string, byte[]>())
            {
                manifest.Append("<manifest:file-entry manifest:full-path=\"Pictures/" + picture.Key + "\" manifest:media-type=\"image/png\"/>");
            }

            // Custom document properties (File, Properties, Custom Properties) live in meta.xml as user-defined fields
            string meta = null;
            if (settings != null)
            {
                var properties = new StringBuilder();
                foreach (var setting in settings)
                {
                    properties.Append("<meta:user-defined meta:name=\"" + setting.Key + "\">" + new XText(setting.Value) + "</meta:user-defined>");
                }

                meta = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-meta " + Namespaces + " xmlns:meta=\"urn:oasis:names:tc:opendocument:xmlns:meta:1.0\" office:version=\"1.3\"><office:meta>" + properties + "</office:meta></office:document-meta>";
                manifest.Append("<manifest:file-entry manifest:full-path=\"meta.xml\" manifest:media-type=\"text/xml\"/>");
            }

            manifest.Append("</manifest:manifest>");

            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (withMimetype)
                {
                    Add(zip, "mimetype", Encoding.ASCII.GetBytes(MediaType), CompressionLevel.NoCompression);
                }

                Add(zip, "META-INF/manifest.xml", Bytes(manifest.ToString()), CompressionLevel.Optimal);
                Add(zip, "content.xml", Bytes(content), CompressionLevel.Optimal);
                Add(zip, "styles.xml", Bytes(styles), CompressionLevel.Optimal);

                if (meta != null)
                {
                    Add(zip, "meta.xml", Bytes(meta), CompressionLevel.Optimal);
                }

                foreach (var picture in pictures ?? new Dictionary<string, byte[]>())
                {
                    Add(zip, "Pictures/" + picture.Key, picture.Value, CompressionLevel.NoCompression);
                }
            }

            return output.ToArray();
        }

        public static IReadOnlyList<PackageEntry> Entries(byte[] package)
        {
            using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
            return zip.Entries.Select(entry =>
            {
                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                return new PackageEntry(entry.FullName, entry.CompressedLength == entry.Length, buffer.ToArray());
            }).ToList();
        }

        public static PackageEntry Entry(byte[] package, string name) => Entries(package).Single(v => v.Name == name);

        /// <summary>The parsed content.xml; parsing throws when the rendering produced malformed XML.</summary>
        public static XDocument Content(byte[] package) => Xml(Entry(package, "content.xml").Bytes);

        public static XDocument Styles(byte[] package) => Xml(Entry(package, "styles.xml").Bytes);

        public static XDocument Manifest(byte[] package) => Xml(Entry(package, "META-INF/manifest.xml").Bytes);

        /// <summary>The text of the paragraphs of a document or an element as a reader shows it, one paragraph per line.</summary>
        public static string Paragraphs(XContainer container) => string.Join("\n", container.Descendants(Text + "p").Select(ReaderText));

        /// <summary>
        /// The text a reader shows for an element: a text:tab reads as a tab, a text:line-break as a line break and a text:s as its spaces.
        /// What sits directly in a drawing element or in embedded binary data, white space or base64, is no text.
        /// </summary>
        public static string ReaderText(XElement element)
        {
            var text = new StringBuilder();
            foreach (var node in element.DescendantNodes())
            {
                if (node is XText value)
                {
                    if (value.Parent.Name.Namespace != Draw && value.Parent.Name.Namespace != Office)
                    {
                        text.Append(value.Value);
                    }
                }
                else if (node is XElement child)
                {
                    if (child.Name == Text + "tab")
                    {
                        text.Append('\t');
                    }
                    else if (child.Name == Text + "line-break")
                    {
                        text.Append('\n');
                    }
                    else if (child.Name == Text + "s")
                    {
                        text.Append(' ', (int?)child.Attribute(Text + "c") ?? 1);
                    }
                }
            }

            return text.ToString();
        }

        private static byte[] Bytes(string text) => new UTF8Encoding(false).GetBytes(text);

        /// <summary>
        /// Parses XML with every white space character kept, as a reader does; a byte order mark, should a part carry one, is detected.
        /// </summary>
        private static XDocument Xml(byte[] bytes)
        {
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true);
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }

        private static void Add(ZipArchive zip, string name, byte[] bytes, CompressionLevel level)
        {
            var entry = zip.CreateEntry(name, level);
            using var stream = entry.Open();
            stream.Write(bytes, 0, bytes.Length);
        }

        internal sealed class PackageEntry
        {
            public PackageEntry(string name, bool stored, byte[] bytes)
            {
                this.Name = name;
                this.Stored = stored;
                this.Bytes = bytes;
            }

            public string Name { get; }

            public bool Stored { get; }

            public byte[] Bytes { get; }
        }
    }
}
