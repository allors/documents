// <copyright file="Flat.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Xml;

    /// <summary>
    /// A flat OpenDocument text (.fodt): the whole document in one XML file, an <c>office:document</c> whose sections hold the meta data, the
    /// settings, the styles and the body, with every picture embedded as base64 <c>office:binary-data</c>. Reading one splits it into the parts
    /// of a package, the form the engine renders: the body and the automatic styles it uses go to content.xml, the styles and the master pages
    /// with the automatic styles they use to styles.xml, the meta data to meta.xml, the settings to settings.xml, and every embedded picture
    /// becomes a file under Pictures/ that its element links to. Writing one merges the parts of a rendered package back into one document.
    /// </summary>
    internal static class Flat
    {
        private const string OfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        private const string StyleNamespace = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        private const string DrawingNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private const string XLinkNamespace = "http://www.w3.org/1999/xlink";
        private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";
        private const string TextMediaType = "application/vnd.oasis.opendocument.text";
        private const string PicturesFolder = "Pictures/";
        private const string SectionBreak = "\n ";
        private const int Base64LineLength = 72;

        /// <summary>
        /// Reads a flat document into the parts of a package. Throws <see cref="InvalidDataException"/> when the bytes are not XML, when the
        /// root element is not office:document or when embedded binary data is not base64.
        /// </summary>
        public static Parts Read(byte[] document)
        {
            XmlDocument xml;
            try
            {
                xml = Inspector.Load(document);
            }
            catch (XmlException e)
            {
                throw new InvalidDataException("the document is neither an OpenDocument package nor a flat OpenDocument text: " + e.Message, e);
            }

            var root = xml.DocumentElement;
            if (root == null || !Is(root, OfficeNamespace, "document"))
            {
                throw new InvalidDataException("the document is not a flat OpenDocument text: its root element is " + (root?.Name ?? "missing") + ", not office:document");
            }

            var mimetype = root.GetAttribute("mimetype", OfficeNamespace);
            if (mimetype.Length == 0)
            {
                mimetype = TextMediaType;
            }

            var version = root.GetAttribute("version", OfficeNamespace);
            var files = new Dictionary<string, byte[]> { [OpenDocumentTemplate.MimetypeFileName] = Encoding.ASCII.GetBytes(mimetype) };
            var pictures = ExtractPictures(xml, files);

            var scripts = Child(root, "scripts");
            var fontFaces = Child(root, "font-face-decls");
            var styles = Child(root, "styles");
            var automaticStyles = Child(root, "automatic-styles");
            var masterStyles = Child(root, "master-styles");
            var body = Child(root, "body");
            var (contentStyles, stylesStyles) = Partition(automaticStyles, body, styles, masterStyles);

            var content = Part(root, "document-content", new[] { scripts, fontFaces, automaticStyles, body }, contentStyles);
            var parts = new List<string> { OpenDocumentTemplate.ContentFileName };

            byte[] stylesPart = null;
            if (fontFaces != null || styles != null || automaticStyles != null || masterStyles != null)
            {
                stylesPart = Part(root, "document-styles", new[] { fontFaces, styles, automaticStyles, masterStyles }, stylesStyles);
                parts.Add(OpenDocumentTemplate.StylesFileName);
            }

            foreach (var (name, part) in new[] { ("meta", OpenDocumentTemplate.DocumentMetaFileName), ("settings", OpenDocumentTemplate.SettingsFileName) })
            {
                var section = Child(root, name);
                if (section != null)
                {
                    files.Add(part, Part(root, "document-" + name, new[] { section }, null));
                    parts.Add(part);
                }
            }

            return new Parts(content, stylesPart, Manifest(mimetype, version, parts, pictures), files);
        }

        /// <summary>
        /// Writes the parts of a rendered document as one flat document: office:document with the media type and the version, then the meta
        /// data, the settings, the scripts, the font faces of both parts, the styles, the automatic styles of both parts, the master pages and
        /// the body, each section starting a line of its own. Every element that links to a picture file of the package embeds the picture's
        /// bytes as base64 instead. A file no element links to, such as the thumbnail, has no place in a flat document.
        /// </summary>
        public static byte[] Write(byte[] mimetype, byte[] content, byte[] styles, IReadOnlyDictionary<string, byte[]> files)
        {
            var contentRoot = Inspector.Load(content).DocumentElement;
            var stylesRoot = styles == null ? null : Inspector.Load(styles).DocumentElement;
            var metaRoot = files.TryGetValue(OpenDocumentTemplate.DocumentMetaFileName, out var meta) ? Inspector.Load(meta).DocumentElement : null;
            var settingsRoot = files.TryGetValue(OpenDocumentTemplate.SettingsFileName, out var settings) ? Inspector.Load(settings).DocumentElement : null;
            var mediaType = mimetype == null ? TextMediaType : Encoding.ASCII.GetString(mimetype).Trim();

            return XmlOutput.Write(writer => new Merger(writer, files).Write(contentRoot, stylesRoot, metaRoot, settingsRoot, mediaType));
        }

        /// <summary>
        /// Every embedded picture leaves its element for a file under Pictures/, named by a hash of its bytes so that a picture embedded twice is
        /// one file, with the extension and the media type its bytes say; the element links to the file the way a package has it.
        /// Returns the files with their media types, in document order.
        /// </summary>
        private static List<KeyValuePair<string, string>> ExtractPictures(XmlDocument xml, Dictionary<string, byte[]> files)
        {
            var pictures = new List<KeyValuePair<string, string>>();
            var xlink = xml.DocumentElement.GetPrefixOfNamespace(XLinkNamespace);
            if (string.IsNullOrEmpty(xlink))
            {
                xlink = "xlink";
            }

            foreach (var data in xml.GetElementsByTagName("binary-data", OfficeNamespace).Cast<XmlElement>().ToList())
            {
                if (!(data.ParentNode is XmlElement holder))
                {
                    continue;
                }

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(data.InnerText);
                }
                catch (FormatException e)
                {
                    throw new InvalidDataException("the document embeds binary data that is not base64: " + e.Message, e);
                }

                var format = PictureFormat.Sniff(bytes);
                var name = PicturesFolder + Hash(bytes) + "." + (format?.Extension ?? "bin");
                if (!files.ContainsKey(name))
                {
                    files.Add(name, bytes);
                    pictures.Add(new KeyValuePair<string, string>(name, format?.MediaType ?? "application/octet-stream"));
                }

                Link(holder, xlink, name);
                RemoveWithWhitespaceBefore(data);
                if (holder.ChildNodes.Cast<XmlNode>().All(IsWhitespace))
                {
                    while (holder.HasChildNodes)
                    {
                        holder.RemoveChild(holder.FirstChild);
                    }
                }
            }

            return pictures;
        }

        /// <summary>The element links to the file, as Writer writes a link: the reference, and the link's type, show and actuate unless the author set them.</summary>
        private static void Link(XmlElement holder, string prefix, string href)
        {
            Set(holder, prefix, "href", href);
            foreach (var (name, value) in new[] { ("type", "simple"), ("show", "embed"), ("actuate", "onLoad") })
            {
                if (!holder.HasAttribute(name, XLinkNamespace))
                {
                    Set(holder, prefix, name, value);
                }
            }
        }

        private static void Set(XmlElement element, string prefix, string localName, string value)
        {
            var attribute = element.OwnerDocument.CreateAttribute(prefix, localName, XLinkNamespace);
            attribute.Value = value;
            element.SetAttributeNode(attribute);
        }

        /// <summary>
        /// Which automatic styles the body uses and which the styles and the master pages use, each closed over the automatic styles those
        /// use in turn. A style both sides use goes to both parts; a style neither uses goes with the body.
        /// </summary>
        private static (HashSet<XmlElement> Content, HashSet<XmlElement> Styles) Partition(XmlElement automaticStyles, XmlElement body, XmlElement styles, XmlElement masterStyles)
        {
            var definitions = new Dictionary<string, List<XmlElement>>(StringComparer.Ordinal);
            var all = automaticStyles?.ChildNodes.OfType<XmlElement>().ToList() ?? new List<XmlElement>();
            foreach (var style in all)
            {
                var name = style.GetAttribute("name", StyleNamespace);
                if (name.Length > 0)
                {
                    if (!definitions.TryGetValue(name, out var named))
                    {
                        definitions[name] = named = new List<XmlElement>();
                    }

                    named.Add(style);
                }
            }

            var content = Closure(definitions, References(body));
            var forStyles = Closure(definitions, References(styles).Concat(References(masterStyles)));
            foreach (var style in all)
            {
                if (!content.Contains(style) && !forStyles.Contains(style))
                {
                    content.Add(style);
                }
            }

            return (content, forStyles);
        }

        private static HashSet<XmlElement> Closure(Dictionary<string, List<XmlElement>> definitions, IEnumerable<string> seeds)
        {
            var used = new HashSet<XmlElement>();
            var queue = new Queue<string>(seeds);
            while (queue.Count > 0)
            {
                if (!definitions.TryGetValue(queue.Dequeue(), out var named))
                {
                    continue;
                }

                foreach (var style in named)
                {
                    if (used.Add(style))
                    {
                        foreach (var reference in References(style))
                        {
                            queue.Enqueue(reference);
                        }
                    }
                }
            }

            return used;
        }

        /// <summary>The names of the styles an element and its descendants refer to: the value of every attribute that names a style or a page layout.</summary>
        private static IEnumerable<string> References(XmlElement element)
        {
            if (element == null)
            {
                yield break;
            }

            foreach (var descendant in new[] { element }.Concat(element.GetElementsByTagName("*").Cast<XmlElement>()))
            {
                foreach (XmlAttribute attribute in descendant.Attributes)
                {
                    if (attribute.Value.Length > 0 && attribute.NamespaceURI != XmlnsNamespace && (attribute.LocalName.EndsWith("style-name", StringComparison.Ordinal) || attribute.LocalName == "page-layout-name"))
                    {
                        yield return attribute.Value;
                    }
                }
            }
        }

        /// <summary>
        /// A part of the package: a root element with the namespaces and the version of the flat document, holding copies of the sections the
        /// document has, in order; the automatic styles section keeps only the styles the part uses.
        /// </summary>
        private static byte[] Part(XmlElement root, string name, IEnumerable<XmlElement> sections, HashSet<XmlElement> automaticStyles)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            var element = document.CreateElement(root.Prefix, name, OfficeNamespace);
            foreach (XmlAttribute attribute in root.Attributes)
            {
                if (attribute.NamespaceURI == XmlnsNamespace || Is(attribute, OfficeNamespace, "version"))
                {
                    element.Attributes.Append((XmlAttribute)document.ImportNode(attribute, true));
                }
            }

            document.AppendChild(element);
            foreach (var section in sections.Where(v => v != null))
            {
                var copy = (XmlElement)document.ImportNode(section, true);
                if (Is(section, OfficeNamespace, "automatic-styles"))
                {
                    Prune(section, copy, automaticStyles);
                }

                element.AppendChild(copy);
            }

            return XmlOutput.Write(writer => element.WriteTo(writer));
        }

        /// <summary>Drops from the copy every style that is not kept, with the white space that indented it.</summary>
        private static void Prune(XmlElement original, XmlElement copy, HashSet<XmlElement> keep)
        {
            var originals = original.ChildNodes.Cast<XmlNode>().ToList();
            var copies = copy.ChildNodes.Cast<XmlNode>().ToList();
            for (var i = 0; i < originals.Count; i++)
            {
                if (originals[i] is XmlElement style && !keep.Contains(style))
                {
                    RemoveWithWhitespaceBefore(copies[i]);
                }
            }
        }

        /// <summary>The manifest of the package the flat document becomes: the document, its parts and its pictures with their media types.</summary>
        private static byte[] Manifest(string mimetype, string version, IEnumerable<string> parts, IEnumerable<KeyValuePair<string, string>> pictures)
        {
            var ns = Allors.Documents.Manifest.Namespace;
            return XmlOutput.Write(writer =>
            {
                writer.WriteStartElement("manifest", "manifest", ns);
                writer.WriteAttributeString("xmlns", "manifest", null, ns);
                if (version.Length > 0)
                {
                    writer.WriteAttributeString("manifest", "version", ns, version);
                }

                Entry(writer, "/", mimetype, version);
                foreach (var part in parts)
                {
                    Entry(writer, part, "text/xml", null);
                }

                foreach (var picture in pictures)
                {
                    Entry(writer, picture.Key, picture.Value, null);
                }

                writer.WriteWhitespace("\n");
                writer.WriteEndElement();
            });
        }

        private static void Entry(XmlWriter writer, string fullPath, string mediaType, string version)
        {
            var ns = Allors.Documents.Manifest.Namespace;
            writer.WriteWhitespace(SectionBreak);
            writer.WriteStartElement("manifest", "file-entry", ns);
            writer.WriteAttributeString("manifest", "full-path", ns, fullPath);
            if (!string.IsNullOrEmpty(version))
            {
                writer.WriteAttributeString("manifest", "version", ns, version);
            }

            writer.WriteAttributeString("manifest", "media-type", ns, mediaType);
            writer.WriteEndElement();
        }

        /// <summary>The bytes as base64 in lines of 72 characters, as Writer embeds a picture, the end tag starting a line of its own.</summary>
        private static string Base64(byte[] bytes)
        {
            var text = Convert.ToBase64String(bytes);
            var lines = new StringBuilder(text.Length + (text.Length / Base64LineLength) + 2);
            for (var at = 0; at < text.Length; at += Base64LineLength)
            {
                lines.Append(text, at, Math.Min(Base64LineLength, text.Length - at)).Append('\n');
            }

            return lines.ToString();
        }

        /// <summary>The first 32 hexadecimal digits of the SHA-256 of the bytes: the name a picture file takes, the same for the same bytes.</summary>
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var text = new StringBuilder(32);
                for (var i = 0; i < 16; i++)
                {
                    text.Append(hash[i].ToString("X2"));
                }

                return text.ToString();
            }
        }

        private static void RemoveWithWhitespaceBefore(XmlNode node)
        {
            var parent = node.ParentNode;
            var before = node.PreviousSibling;
            parent.RemoveChild(node);
            if (IsWhitespace(before))
            {
                parent.RemoveChild(before);
            }
        }

        private static XmlElement Child(XmlElement parent, string localName) => parent?.ChildNodes.OfType<XmlElement>().FirstOrDefault(v => Is(v, OfficeNamespace, localName));

        private static bool Is(XmlNode node, string ns, string localName) => node is XmlElement element && element.LocalName == localName && element.NamespaceURI == ns;

        private static bool Is(XmlAttribute attribute, string ns, string localName) => attribute.LocalName == localName && attribute.NamespaceURI == ns;

        private static bool IsWhitespace(XmlNode node) => node is XmlWhitespace || node is XmlSignificantWhitespace;

        /// <summary>Writes the parts of one document as one flat document, embedding the pictures the elements link to.</summary>
        private sealed class Merger
        {
            private readonly XmlWriter writer;
            private readonly IReadOnlyDictionary<string, byte[]> files;

            public Merger(XmlWriter writer, IReadOnlyDictionary<string, byte[]> files)
            {
                this.writer = writer;
                this.files = files;
            }

            public void Write(XmlElement content, XmlElement styles, XmlElement meta, XmlElement settings, string mediaType)
            {
                var roots = new[] { content, styles, meta, settings }.Where(v => v != null).ToList();
                this.writer.WriteStartElement(content.Prefix, "document", OfficeNamespace);

                // The namespaces of every part, each prefix declared once, in the order the parts declare them
                var declared = new HashSet<string>(StringComparer.Ordinal);
                foreach (var root in roots)
                {
                    foreach (XmlAttribute attribute in root.Attributes)
                    {
                        if (attribute.NamespaceURI == XmlnsNamespace && declared.Add(attribute.Name))
                        {
                            this.writer.WriteAttributeString(attribute.Prefix, attribute.LocalName, attribute.NamespaceURI, attribute.Value);
                        }
                    }
                }

                var version = roots.Select(v => v.GetAttribute("version", OfficeNamespace)).FirstOrDefault(v => v.Length > 0);
                if (version != null)
                {
                    this.writer.WriteAttributeString(content.Prefix, "version", OfficeNamespace, version);
                }

                this.writer.WriteAttributeString(content.Prefix, "mimetype", OfficeNamespace, mediaType);

                this.Section(Child(meta, "meta"));
                this.Section(Child(settings, "settings"));
                this.Section(Child(content, "scripts"));
                this.Merged(Child(content, "font-face-decls"), Child(styles, "font-face-decls"), FontFaceKey);
                this.Section(Child(styles, "styles"));
                this.Merged(Child(content, "automatic-styles"), Child(styles, "automatic-styles"), StyleKey);
                this.Section(Child(styles, "master-styles"));
                this.Section(Child(content, "body"));

                this.writer.WriteWhitespace("\n");
                this.writer.WriteFullEndElement();
            }

            private static string FontFaceKey(XmlElement fontFace) => fontFace.GetAttribute("name", StyleNamespace);

            private static string StyleKey(XmlElement style) => style.Name + "|" + style.GetAttribute("name", StyleNamespace) + "|" + style.GetAttribute("family", StyleNamespace);

            private static bool CanEmbed(XmlElement element) =>
                Is(element, DrawingNamespace, "image") || Is(element, DrawingNamespace, "object-ole") || Is(element, StyleNamespace, "background-image") || Is(element, TextNamespace, "list-level-style-image");

            private void Section(XmlElement section)
            {
                if (section != null)
                {
                    this.writer.WriteWhitespace(SectionBreak);
                    this.WriteElement(section);
                }
            }

            /// <summary>Two sections of one kind as one: the first's children, then those of the second whose key the first did not have.</summary>
            private void Merged(XmlElement first, XmlElement second, Func<XmlElement, string> key)
            {
                if (first == null || second == null)
                {
                    this.Section(first ?? second);
                    return;
                }

                this.writer.WriteWhitespace(SectionBreak);
                this.writer.WriteStartElement(first.Prefix, first.LocalName, first.NamespaceURI);
                this.WriteAttributes(first, null);

                var keys = new HashSet<string>(StringComparer.Ordinal);
                for (var child = first.FirstChild; child != null; child = child.NextSibling)
                {
                    if (child is XmlElement element)
                    {
                        keys.Add(key(element));
                    }

                    if (child.NextSibling != null || !IsWhitespace(child))
                    {
                        this.WriteNode(child);
                    }
                }

                XmlNode pending = null;
                for (var child = second.FirstChild; child != null; child = child.NextSibling)
                {
                    if (IsWhitespace(child))
                    {
                        pending = child;
                        continue;
                    }

                    if (child is XmlElement element && !keys.Add(key(element)))
                    {
                        pending = null;
                        continue;
                    }

                    this.WritePending(ref pending);
                    this.WriteNode(child);
                }

                this.WritePending(ref pending);
                this.writer.WriteFullEndElement();
            }

            private void WritePending(ref XmlNode pending)
            {
                if (pending != null)
                {
                    this.WriteNode(pending);
                    pending = null;
                }
            }

            private void WriteNode(XmlNode node)
            {
                switch (node.NodeType)
                {
                    case XmlNodeType.Element:
                        this.WriteElement((XmlElement)node);
                        break;

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        this.writer.WriteString(node.Value);
                        break;

                    case XmlNodeType.Comment:
                        this.writer.WriteComment(node.Value);
                        break;

                    case XmlNodeType.ProcessingInstruction:
                        this.writer.WriteProcessingInstruction(node.Name, node.Value);
                        break;
                }
            }

            /// <summary>An element as it is, except that one linking to a picture file of the package embeds the picture instead of the link.</summary>
            private void WriteElement(XmlElement element)
            {
                var picture = this.EmbeddedPicture(element);
                this.writer.WriteStartElement(element.Prefix, element.LocalName, element.NamespaceURI);
                this.WriteAttributes(element, picture);
                if (picture != null)
                {
                    var prefix = element.GetPrefixOfNamespace(OfficeNamespace);
                    this.writer.WriteStartElement(string.IsNullOrEmpty(prefix) ? "office" : prefix, "binary-data", OfficeNamespace);
                    this.writer.WriteString(Base64(picture));
                    this.writer.WriteFullEndElement();
                }

                for (var child = element.FirstChild; child != null; child = child.NextSibling)
                {
                    if (picture == null || !Is(child, OfficeNamespace, "binary-data"))
                    {
                        this.WriteNode(child);
                    }
                }

                this.writer.WriteFullEndElement();
            }

            private void WriteAttributes(XmlElement element, byte[] picture)
            {
                foreach (XmlAttribute attribute in element.Attributes)
                {
                    if (picture == null || attribute.NamespaceURI != XLinkNamespace)
                    {
                        this.writer.WriteAttributeString(attribute.Prefix, attribute.LocalName, attribute.NamespaceURI, attribute.Value);
                    }
                }
            }

            /// <summary>The bytes of the package file the element links to, when the element is one that may embed them; null otherwise.</summary>
            private byte[] EmbeddedPicture(XmlElement element)
            {
                if (!CanEmbed(element))
                {
                    return null;
                }

                var href = element.GetAttribute("href", XLinkNamespace);
                if (href.StartsWith("./", StringComparison.Ordinal))
                {
                    href = href.Substring(2);
                }

                return href.Length > 0 && this.files.TryGetValue(href, out var bytes) ? bytes : null;
            }
        }
    }
}
