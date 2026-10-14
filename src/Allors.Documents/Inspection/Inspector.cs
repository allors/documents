// <copyright file="Inspector.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Inspection
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using Allors.Document.Tags;

    /// <summary>Reads the tags, bound pictures and settings of a template package without rendering it.</summary>
    internal static class Inspector
    {
        private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private const string DrawingNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        private const string MetaNamespace = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

        private static readonly Regex WhitespaceRegex = new Regex(@"\s+");

        public static TemplateInspection Inspect(byte[] package)
        {
            var parts = new Dictionary<string, byte[]>();
            using (var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName == "content.xml" || entry.FullName == "styles.xml" || entry.FullName == "meta.xml")
                    {
                        using (var stream = entry.Open())
                        using (var buffer = new MemoryStream())
                        {
                            stream.CopyTo(buffer);
                            parts[entry.FullName] = buffer.ToArray();
                        }
                    }
                }
            }

            var tags = new List<InspectedTag>();
            var pictures = new List<InspectedPicture>();
            var problems = new List<InspectionProblem>();

            foreach (var part in new[] { "content.xml", "styles.xml" })
            {
                if (parts.TryGetValue(part, out var xml))
                {
                    new PartWalker(part, Load(xml), tags, pictures, problems).Walk();
                }
            }

            var settings = new Dictionary<string, string>();
            if (parts.TryGetValue("meta.xml", out var meta))
            {
                foreach (XmlElement element in Load(meta).GetElementsByTagName("user-defined", MetaNamespace))
                {
                    var name = element.GetAttribute("name", MetaNamespace);
                    if (!string.IsNullOrEmpty(name))
                    {
                        settings[name] = element.InnerText;
                    }
                }
            }

            return new TemplateInspection(tags, pictures, settings, problems);
        }

        private static XmlDocument Load(byte[] xml)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            using (var stream = new MemoryStream(xml))
            {
                document.Load(stream);
            }

            return document;
        }

        private sealed class PartWalker
        {
            private readonly string part;
            private readonly XmlDocument document;
            private readonly List<InspectedTag> tags;
            private readonly List<InspectedPicture> pictures;
            private readonly List<InspectionProblem> problems;
            private readonly Stack<InspectedTag> blocks = new Stack<InspectedTag>();
            private readonly Dictionary<XmlElement, int> paragraphOrdinals = new Dictionary<XmlElement, int>();

            public PartWalker(string part, XmlDocument document, List<InspectedTag> tags, List<InspectedPicture> pictures, List<InspectionProblem> problems)
            {
                this.part = part;
                this.document = document;
                this.tags = tags;
                this.pictures = pictures;
                this.problems = problems;
            }

            public void Walk()
            {
                this.Visit(this.document.DocumentElement);

                while (this.blocks.Count > 0)
                {
                    var block = this.blocks.Pop();
                    this.problems.Add(new InspectionProblem(block.Tag.Text, "this block never ends: add an <@end> tag", block.Location));
                }
            }

            private void Visit(XmlElement element)
            {
                if (element.NamespaceURI == TextNamespace && (element.LocalName == "p" || element.LocalName == "h"))
                {
                    this.paragraphOrdinals[element] = this.paragraphOrdinals.Count + 1;
                }

                if (element.NamespaceURI == TextNamespace && element.LocalName == "placeholder")
                {
                    this.VisitPlaceholder(element);
                    return;
                }

                if (element.NamespaceURI == DrawingNamespace && element.LocalName == "frame")
                {
                    this.VisitFrame(element);
                }

                foreach (XmlNode child in element.ChildNodes)
                {
                    if (child is XmlElement childElement)
                    {
                        this.Visit(childElement);
                    }
                }
            }

            private void VisitPlaceholder(XmlElement element)
            {
                var text = element.InnerText.Trim();
                if (!TagSyntax.IsTag(text))
                {
                    return;
                }

                var location = this.LocationOf(element);
                if (!TagSyntax.TryParse(text, out var tag, out var error))
                {
                    this.problems.Add(new InspectionProblem(text, error, location));
                    return;
                }

                var block = this.blocks.Count > 0 ? this.blocks.Peek() : null;

                if (tag.Kind == TagKind.End)
                {
                    if (block == null)
                    {
                        this.problems.Add(new InspectionProblem(text, "this <@end> closes nothing: no loop or condition is open here", location));
                        return;
                    }

                    this.blocks.Pop();
                    block.Add(new InspectedTag(tag, location, block));
                    return;
                }

                var inspected = new InspectedTag(tag, location, block);
                if (block != null)
                {
                    block.Add(inspected);
                }
                else
                {
                    this.tags.Add(inspected);
                }

                if (tag.Kind == TagKind.For || tag.Kind == TagKind.If)
                {
                    this.blocks.Push(inspected);
                }
            }

            private void VisitFrame(XmlElement element)
            {
                var name = element.GetAttribute("name", DrawingNamespace).Trim();
                if (!name.StartsWith("$"))
                {
                    return;
                }

                var location = this.LocationOf(element);
                if (!TagSyntax.TryParsePicture(name, out var path, out var error))
                {
                    this.problems.Add(new InspectionProblem(name, error, location));
                    return;
                }

                this.pictures.Add(new InspectedPicture(name, path, location, this.blocks.Count > 0 ? this.blocks.Peek() : null));
            }

            private Location LocationOf(XmlElement element)
            {
                for (var node = element.ParentNode; node is XmlElement parent; node = parent.ParentNode)
                {
                    if (this.paragraphOrdinals.TryGetValue(parent, out var ordinal))
                    {
                        var text = WhitespaceRegex.Replace(parent.InnerText, " ").Trim();
                        return new Location(this.part, ordinal, text.Length > 200 ? text.Substring(0, 200) : text);
                    }
                }

                return new Location(this.part, 0, string.Empty);
            }
        }
    }
}
