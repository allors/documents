// <copyright file="Inspector.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Inspection
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Xml;
    using Allors.Document.OpenDocument;
    using Allors.Document.Tags;

    /// <summary>Reads the tags, bound pictures and settings of a template package without rendering it.</summary>
    internal static class Inspector
    {
        private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private const string DrawingNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        private const string SvgNamespace = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
        private const string MetaNamespace = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

        private static readonly Regex WhitespaceRegex = new Regex(@"\s+");

        public static TemplateInspection Inspect(byte[] document)
        {
            var parts = Parts.Read(document);

            var tags = new List<InspectedTag>();
            var pictures = new List<InspectedPicture>();
            var problems = new List<InspectionProblem>();

            new PartWalker(OpenDocumentTemplate.ContentFileName, Load(parts.Content), tags, pictures, problems).Walk();
            if (parts.Styles != null)
            {
                new PartWalker(OpenDocumentTemplate.StylesFileName, Load(parts.Styles), tags, pictures, problems).Walk();
            }

            var settings = parts.Files.TryGetValue(OpenDocumentTemplate.DocumentMetaFileName, out var meta) ? Settings(Load(meta)) : new Dictionary<string, string>();

            return new TemplateInspection(tags, pictures, settings, problems);
        }

        /// <summary>Loads a part with every white space character kept: Writer saves a part without indentation, so every one is the author's.</summary>
        internal static XmlDocument Load(byte[] xml)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            using (var stream = new MemoryStream(xml))
            {
                document.Load(stream);
            }

            return document;
        }

        /// <summary>The custom document properties of meta.xml (File, Properties, Custom Properties), by name.</summary>
        internal static IReadOnlyDictionary<string, string> Settings(XmlDocument meta)
        {
            var settings = new Dictionary<string, string>();
            foreach (XmlElement element in meta.GetElementsByTagName("user-defined", MetaNamespace))
            {
                var name = element.GetAttribute("name", MetaNamespace);
                if (!string.IsNullOrEmpty(name))
                {
                    settings[name] = element.InnerText;
                }
            }

            return settings;
        }

        /// <summary>Walks one part and collects its tags as a tree, its bound frames and what cannot be read. The part is not changed.</summary>
        internal sealed class PartWalker
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
                    this.problems.Add(new InspectionProblem(block.Tag.Text, "this block never ends: add an <@end> tag", block.Location, true));
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
                        this.problems.Add(new InspectionProblem(text, "this <@end> closes nothing: no loop or condition is open here", location, true));
                        return;
                    }

                    this.blocks.Pop();
                    block.Add(new InspectedTag(tag, location, block, element));
                    return;
                }

                var inspected = new InspectedTag(tag, location, block, element);
                if (block != null)
                {
                    block.Add(inspected);
                }
                else
                {
                    this.tags.Add(inspected);
                }

                if (tag.Kind == TagKind.For || tag.Kind == TagKind.If || tag.Kind == TagKind.IfDef)
                {
                    this.blocks.Push(inspected);
                }
            }

            /// <summary>A frame whose description holds a tag is a bound picture (Image, Properties, Options, Description); its name stays the author's.</summary>
            private void VisitFrame(XmlElement element)
            {
                var description = Child(element, SvgNamespace, "desc")?.InnerText;
                if (!TagSyntax.HoldsTag(description))
                {
                    return;
                }

                var location = this.LocationOf(element);
                if (Child(element, DrawingNamespace, "image") == null)
                {
                    this.problems.Add(new InspectionProblem(description.Trim(), "this frame holds no picture: a picture tag goes in the description of a picture", location));
                    return;
                }

                if (!TagSyntax.TryParsePicture(description, out var tag, out var error))
                {
                    this.problems.Add(new InspectionProblem(description.Trim(), error, location));
                    return;
                }

                var name = element.GetAttribute("name", DrawingNamespace).Trim();
                this.pictures.Add(new InspectedPicture(name, tag, location, this.blocks.Count > 0 ? this.blocks.Peek() : null, element));
            }

            private static XmlElement Child(XmlElement element, string ns, string localName)
            {
                foreach (XmlNode child in element.ChildNodes)
                {
                    if (child is XmlElement childElement && childElement.NamespaceURI == ns && childElement.LocalName == localName)
                    {
                        return childElement;
                    }
                }

                return null;
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
