// <copyright file="Inspector.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Inspection
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml;
    using Allors.Document.OpenDocument;
    using Allors.Document.Tags;

    /// <summary>Reads the tags, bound pictures, maps and settings of a template package without rendering it.</summary>
    internal static class Inspector
    {
        private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private const string TableNamespace = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        private const string DrawingNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        private const string SvgNamespace = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
        private const string MetaNamespace = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";

        private static readonly Regex WhitespaceRegex = new Regex(@"\s+");

        public static TemplateInspection Inspect(byte[] document)
        {
            var parts = Parts.Read(document);

            var tags = new List<InspectedTag>();
            var pictures = new List<InspectedPicture>();
            var maps = new List<InspectedMap>();
            var problems = new List<InspectionProblem>();

            new PartWalker(OpenDocumentTemplate.ContentFileName, Load(parts.Content), tags, pictures, maps, problems).Walk();
            if (parts.Styles != null)
            {
                new PartWalker(OpenDocumentTemplate.StylesFileName, Load(parts.Styles), tags, pictures, maps, problems).Walk();
            }

            var settings = parts.Files.TryGetValue(OpenDocumentTemplate.DocumentMetaFileName, out var meta) ? Settings(Load(meta)) : new Dictionary<string, string>();

            return new TemplateInspection(tags, pictures, maps, settings, problems);
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

        /// <summary>
        /// The text of an element as a reader shows it: a text:s reads as its spaces, a text:tab as a tab, a text:line-break as a line break,
        /// and every paragraph ends with a line break; what sits in a drawing is no text. Trimmed.
        /// </summary>
        internal static string ReaderText(XmlElement element)
        {
            var text = new StringBuilder();
            CollectText(element, text);
            return text.ToString().Trim();
        }

        private static void CollectText(XmlNode node, StringBuilder text)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                switch (child)
                {
                    case XmlElement element:
                        if (element.NamespaceURI == TextNamespace)
                        {
                            switch (element.LocalName)
                            {
                                case "s":
                                    var count = element.GetAttribute("c", TextNamespace);
                                    text.Append(' ', int.TryParse(count, out var spaces) && spaces > 0 ? spaces : 1);
                                    continue;
                                case "tab":
                                    text.Append('\t');
                                    continue;
                                case "line-break":
                                    text.Append('\n');
                                    continue;
                                case "p":
                                case "h":
                                    CollectText(element, text);
                                    text.Append('\n');
                                    continue;
                            }
                        }
                        else if (element.NamespaceURI == DrawingNamespace)
                        {
                            continue;
                        }

                        CollectText(element, text);
                        break;

                    case XmlText _:
                    case XmlCDataSection _:
                    case XmlWhitespace _:
                    case XmlSignificantWhitespace _:
                        text.Append(child.Value);
                        break;
                }
            }
        }

        /// <summary>Walks one part and collects its tags as a tree, its bound frames, its maps and what cannot be read. The part is not changed.</summary>
        internal sealed class PartWalker
        {
            private readonly string part;
            private readonly XmlDocument document;
            private readonly List<InspectedTag> tags;
            private readonly List<InspectedPicture> pictures;
            private readonly List<InspectedMap> maps;
            private readonly List<InspectionProblem> problems;
            private readonly Stack<InspectedTag> blocks = new Stack<InspectedTag>();
            private readonly Dictionary<XmlElement, int> paragraphOrdinals = new Dictionary<XmlElement, int>();

            public PartWalker(string part, XmlDocument document, List<InspectedTag> tags, List<InspectedPicture> pictures, List<InspectedMap> maps, List<InspectionProblem> problems)
            {
                this.part = part;
                this.document = document;
                this.tags = tags;
                this.pictures = pictures;
                this.maps = maps;
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
                if (IsParagraph(element))
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

                if (Is(element, TableNamespace, "table") && this.TryVisitMap(element))
                {
                    return;
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

                switch (tag.Kind)
                {
                    case TagKind.End:
                        if (block == null)
                        {
                            this.problems.Add(new InspectionProblem(text, "this <@end> closes nothing: no loop or condition is open here", location, true));
                            return;
                        }

                        this.blocks.Pop();
                        block.Add(new InspectedTag(tag, location, block, element));
                        return;

                    case TagKind.Else:
                        if (block == null)
                        {
                            this.problems.Add(new InspectionProblem(text, "this " + Keyword(tag) + " belongs to no condition: no <@if> or <@ifdef> is open here", location, true));
                            return;
                        }

                        if (block.Tag.Kind == TagKind.For)
                        {
                            this.problems.Add(new InspectionProblem(text, "this " + Keyword(tag) + " sits in a loop, which has no else: an else belongs to an <@if> or <@ifdef>", location, true));
                            return;
                        }

                        var previous = block.LastElse;
                        if (previous != null && !previous.Tag.HasTest)
                        {
                            this.problems.Add(new InspectionProblem(text, tag.HasTest ? "this " + Keyword(tag) + " comes after the <@else>: put it before" : "this condition already has an <@else>", location, true));
                            return;
                        }

                        block.Add(new InspectedTag(tag, location, block, element));
                        return;

                    case TagKind.Map:
                        this.problems.Add(new InspectionProblem(text, "a <@map> tag goes in the first cell of a table of two columns: the keys in the first column, the texts in the second", location));
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
                var block = this.blocks.Count > 0 ? this.blocks.Peek() : null;
                this.pictures.Add(new InspectedPicture(name, tag, location, block, block?.LastElse ?? block, element));
            }

            /// <summary>
            /// A table whose first cell holds a map tag is a map: its rows below the first map the text of their first cell to the text of
            /// their second. The table's own paragraphs count in the part, so that locations agree with the template; its tags are not read.
            /// </summary>
            private bool TryVisitMap(XmlElement table)
            {
                var rows = Rows(table);
                var placeholder = rows.Count == 0 ? null : FirstPlaceholder(Cells(rows[0]).FirstOrDefault());
                if (placeholder == null || !TagSyntax.TryParse(placeholder.InnerText, out var tag, out _) || tag.Kind != TagKind.Map)
                {
                    return false;
                }

                this.NumberParagraphs(table);
                var location = this.LocationOf(placeholder);
                var name = tag.Path;

                if (this.maps.Any(v => v.Name == name))
                {
                    this.problems.Add(new InspectionProblem(tag.Text, "the map '" + name + "' is defined twice: the first table counts", location));
                    return true;
                }

                var entries = new Dictionary<string, string>();
                string defaultText = null;
                var hasDefault = false;
                foreach (var row in rows.Skip(1))
                {
                    var cells = Cells(row);
                    var rowLocation = this.LocationOf(cells.Count > 0 ? FirstParagraph(cells[0]) ?? placeholder : placeholder);
                    if (cells.Count < 2)
                    {
                        this.problems.Add(new InspectionProblem(tag.Text, "a row of the map '" + name + "' has one cell: a row holds a key in its first cell and a text in its second", rowLocation));
                        continue;
                    }

                    var tagInside = cells.Take(2).Select(FirstPlaceholder).FirstOrDefault(v => v != null && TagSyntax.IsTag(v.InnerText));
                    if (tagInside != null)
                    {
                        this.problems.Add(new InspectionProblem(tag.Text, "the map '" + name + "' holds the tag '" + tagInside.InnerText.Trim() + "': the keys and texts of a map are plain text", rowLocation));
                        continue;
                    }

                    var key = ReaderText(cells[0]);
                    var text = ReaderText(cells[1]);
                    if (key.Length == 0)
                    {
                        this.problems.Add(new InspectionProblem(tag.Text, "a row of the map '" + name + "' has no key: the first cell is empty", rowLocation));
                    }
                    else if (TagSyntax.IsDefaultKey(key))
                    {
                        if (hasDefault)
                        {
                            this.problems.Add(new InspectionProblem(tag.Text, "the map '" + name + "' has two default rows: the first counts", rowLocation));
                        }
                        else
                        {
                            hasDefault = true;
                            defaultText = text;
                        }
                    }
                    else if (entries.ContainsKey(key))
                    {
                        this.problems.Add(new InspectionProblem(tag.Text, "the map '" + name + "' has two rows for '" + key + "': the first counts", rowLocation));
                    }
                    else
                    {
                        entries.Add(key, text);
                    }
                }

                this.maps.Add(new InspectedMap(tag, entries, defaultText, hasDefault, location, table));
                return true;
            }

            /// <summary>The rows of a table in document order, the rows of a table nested in a cell left out.</summary>
            private static List<XmlElement> Rows(XmlElement table)
            {
                var rows = new List<XmlElement>();
                CollectRows(table, rows);
                return rows;
            }

            private static void CollectRows(XmlElement element, List<XmlElement> rows)
            {
                foreach (XmlNode child in element.ChildNodes)
                {
                    if (!(child is XmlElement childElement) || Is(childElement, TableNamespace, "table"))
                    {
                        continue;
                    }

                    if (Is(childElement, TableNamespace, "table-row"))
                    {
                        rows.Add(childElement);
                    }
                    else if (childElement.NamespaceURI == TableNamespace)
                    {
                        CollectRows(childElement, rows);
                    }
                }
            }

            /// <summary>The cells of a row in order, a covered cell of a merge left out.</summary>
            private static List<XmlElement> Cells(XmlElement row) => row.ChildNodes.OfType<XmlElement>().Where(v => Is(v, TableNamespace, "table-cell")).ToList();

            private static XmlElement FirstPlaceholder(XmlElement element) => element == null ? null : Descendants(element).FirstOrDefault(v => Is(v, TextNamespace, "placeholder"));

            private static XmlElement FirstParagraph(XmlElement element) => Descendants(element).FirstOrDefault(IsParagraph);

            private static IEnumerable<XmlElement> Descendants(XmlElement element)
            {
                foreach (XmlNode child in element.ChildNodes)
                {
                    if (child is XmlElement childElement)
                    {
                        yield return childElement;

                        foreach (var descendant in Descendants(childElement))
                        {
                            yield return descendant;
                        }
                    }
                }
            }

            private void NumberParagraphs(XmlElement element)
            {
                foreach (var descendant in Descendants(element))
                {
                    if (IsParagraph(descendant))
                    {
                        this.paragraphOrdinals[descendant] = this.paragraphOrdinals.Count + 1;
                    }
                }
            }

            private static bool IsParagraph(XmlElement element) => element.NamespaceURI == TextNamespace && (element.LocalName == "p" || element.LocalName == "h");

            private static bool Is(XmlElement element, string ns, string localName) => element.NamespaceURI == ns && element.LocalName == localName;

            private static string Keyword(Tag tag) => tag.HasTest ? (tag.Defined ? "<@else ifdef>" : "<@else if>") : "<@else>";

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

            /// <summary>Where an element sits: the paragraph it is, or the nearest paragraph that holds it.</summary>
            private Location LocationOf(XmlElement element)
            {
                for (var node = (XmlNode)element; node is XmlElement parent; node = parent.ParentNode)
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
