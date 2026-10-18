// <copyright file="Renderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Allors.Document.Inspection;
    using Allors.Document.Models;
    using Allors.Document.OpenDocument;
    using Allors.Document.Tags;
    using Allors.Document.Validation;
    using Allors.Document.Xml;
    using Formatting = Allors.Document.Models.Formatting;

    /// <summary>
    /// Renders programs for one model: walks a program's DOM and writes it as XML, with every binding replaced by its formatted value,
    /// every condition reduced to the branch it takes, every loop repeated and every bound picture frame showing its picture, and reports what the
    /// template asks that the model cannot give. One renderer serves one render, both parts; a program is shared between renders and never changed.
    /// The output reads as the template did: its declaration, no byte order mark, a full end tag on every element.
    /// </summary>
    internal sealed class Renderer
    {
        private const string DrawingNamespace = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
        private const string SvgNamespace = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
        private const string OfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        private const string LoExtNamespace = "urn:org:documentfoundation:names:experimental:office:xmlns:loext:1.0";
        private const string StyleNamespace = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        private const string XLinkNamespace = "http://www.w3.org/1999/xlink";

        private static readonly IReadOnlyDictionary<string, InspectedMap> NoMaps = new Dictionary<string, InspectedMap>();

        /// <summary>The layer that takes a renderer's name out of the formatting before the resolver sees it: a name is no .NET format string.</summary>
        private static readonly KeyValuePair<string, string>[] NoFormat = { new KeyValuePair<string, string>(Formatting.FormatAttribute, null) };

        private readonly IResolver resolver;
        private readonly Formatting formatting;
        private readonly Formatting counterFormatting;
        private readonly object model;
        private readonly Pictures pictures;
        private readonly IReadOnlyDictionary<string, InspectedMap> maps;
        private readonly Renderers renderers;
        private readonly IPictureProcessor processor;
        private readonly List<Finding> findings = new List<Finding>();
        private readonly HashSet<(Instruction, FindingKind, string)> reported = new HashSet<(Instruction, FindingKind, string)>();
        private readonly HashSet<string> frameNames = new HashSet<string>();
        private readonly HashSet<string> hrefs = new HashSet<string>();

        private Program program;
        private XmlWriter writer;

        public Renderer(IResolver resolver, Formatting formatting, object model, Pictures pictures, IReadOnlyDictionary<string, InspectedMap> maps = null, Renderers renderers = null, IPictureProcessor processor = null)
        {
            this.resolver = resolver;
            this.formatting = formatting;
            this.model = model;
            this.pictures = pictures;
            this.maps = maps ?? NoMaps;
            this.renderers = renderers ?? new Renderers();
            this.processor = processor;

            // The number pattern is for the model's amounts; a loop counter prints as a plain integer unless its tag says otherwise
            this.counterFormatting = formatting.With(new[] { new KeyValuePair<string, string>(Formatting.NumberSetting, null) });
        }

        /// <summary>Every finding of the parts rendered so far, in render order; a member, a loop, a key or a picture is reported once per tag.</summary>
        public IReadOnlyList<Finding> Findings => this.findings;

        /// <summary>The pictures the parts rendered so far put into the package.</summary>
        public Pictures Pictures => this.pictures;

        /// <summary>Every file the parts rendered so far link to, by its xlink:href. A template picture outside this set is shown nowhere.</summary>
        public ICollection<string> Hrefs => this.hrefs;

        /// <summary>The part as the document holds it, with every instruction executed for the model.</summary>
        public byte[] Render(Program program)
        {
            this.program = program;
            this.findings.AddRange(program.Findings);

            return XmlOutput.Write(xmlWriter =>
            {
                this.writer = xmlWriter;
                this.Write(program.Document.DocumentElement, new Scope(this.model));
            });
        }

        private static bool IsFrame(XmlElement element) => element.LocalName == "frame" && element.NamespaceURI == DrawingNamespace;

        private static bool IsName(XmlAttribute attribute) => attribute.LocalName == "name" && attribute.NamespaceURI == DrawingNamespace;

        private static bool IsHref(XmlAttribute attribute) => attribute.LocalName == "href" && attribute.NamespaceURI == XLinkNamespace;

        private static bool Is(XmlNode node, string ns, string localName) => node is XmlElement element && element.LocalName == localName && element.NamespaceURI == ns;

        private static bool Is(XmlAttribute attribute, string ns, string localName) => attribute.LocalName == localName && attribute.NamespaceURI == ns;

        private static bool IsBranch(XmlNode node) => Is(node, Program.Namespace, Program.ElseName);

        private void Write(XmlNode node, Scope scope)
        {
            switch (node.NodeType)
            {
                case XmlNodeType.Element:
                    var element = (XmlElement)node;
                    if (this.program.Instructions.TryGetValue(element, out var instruction))
                    {
                        this.Execute(instruction, element, scope);
                    }
                    else
                    {
                        this.WriteElement(element, scope);
                    }

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

        private void WriteElement(XmlElement element, Scope scope)
        {
            this.writer.WriteStartElement(element.Prefix, element.LocalName, element.NamespaceURI);
            this.WriteAttributes(element, IsFrame(element));
            this.WriteChildren(element, scope);
            this.writer.WriteFullEndElement();
        }

        /// <summary>
        /// Attributes pass as they are, except that a frame's name is made unique in the document, as Writer requires of frames, and that a side of a
        /// frame a fit changed takes its new length and loses its relative size, which the length replaces; every link is noted.
        /// </summary>
        private void WriteAttributes(XmlElement element, bool isFrame, string width = null, string height = null)
        {
            if (!element.HasAttributes)
            {
                return;
            }

            foreach (XmlAttribute attribute in element.Attributes)
            {
                var value = attribute.Value;
                if (isFrame && IsName(attribute))
                {
                    value = this.UniqueFrameName(value);
                }
                else if (IsHref(attribute))
                {
                    this.hrefs.Add(value);
                }
                else if (width != null && Is(attribute, SvgNamespace, "width"))
                {
                    value = width;
                }
                else if (height != null && Is(attribute, SvgNamespace, "height"))
                {
                    value = height;
                }
                else if ((width != null && Is(attribute, StyleNamespace, "rel-width")) || (height != null && Is(attribute, StyleNamespace, "rel-height")))
                {
                    continue;
                }

                this.writer.WriteAttributeString(attribute.Prefix, attribute.LocalName, attribute.NamespaceURI, value);
            }
        }

        private void WriteChildren(XmlElement element, Scope scope)
        {
            for (var child = element.FirstChild; child != null; child = child.NextSibling)
            {
                this.Write(child, scope);
            }
        }

        private void Execute(Instruction instruction, XmlElement element, Scope scope)
        {
            switch (instruction.Tag.Kind)
            {
                case TagKind.Binding:
                    this.WriteBinding(instruction, scope);
                    break;

                case TagKind.Picture:
                    this.WritePicture(instruction, element, scope);
                    break;

                case TagKind.If:
                case TagKind.IfDef:
                    this.WriteCondition(instruction, element, scope);
                    break;

                case TagKind.For:
                    this.WriteLoop(instruction, element, scope);
                    break;
            }
        }

        /// <summary>
        /// A value prints as the resolver formats it under the tag's formatting, or as the text its map gives for it; a path that breaks prints nothing.
        /// A loop counter prints as a plain integer, unless the tag carries a format of its own. When what would print is empty or white space, the
        /// tag's blank text prints instead.
        /// </summary>
        private void WriteBinding(Instruction instruction, Scope scope)
        {
            var tag = instruction.Tag;
            var attributes = tag.Attributes;

            string text = null;
            if (this.TryResolve(instruction, tag.Path, scope, out var value) && value != null)
            {
                text = attributes.TryGetValue(TagSyntax.MapAttribute, out var map) ? this.Map(instruction, map, value, scope) : this.Format(instruction, value, scope);
            }

            if (string.IsNullOrWhiteSpace(text) && attributes.TryGetValue(TagSyntax.BlankAttribute, out var blank))
            {
                text = blank;
            }

            ValueWriter.Write(this.writer, text);
        }

        /// <summary>
        /// The value as the resolver formats it under the tag's formatting, then through the text renderer the tag's format names, when it names
        /// one: the name is taken out of the formatting first, so a renderer's name never reaches the resolver as a format string. A format that
        /// names a picture renderer is reported and the value prints without it; a renderer that throws is reported and the value prints blank.
        /// </summary>
        private string Format(Instruction instruction, object value, Scope scope)
        {
            var tag = instruction.Tag;
            var attributes = tag.Attributes;

            ITextRenderer renderer = null;
            var named = false;
            if (attributes.TryGetValue(Formatting.FormatAttribute, out var format) && this.renderers.TryGet(format, out var registered))
            {
                named = true;
                renderer = registered as ITextRenderer;
                if (renderer == null)
                {
                    this.Report(instruction, FindingKind.BadValue, FindingSeverity.Error, tag.Path, Formatting.FormatAttribute + "=\"" + format + "\" renders a picture, not text: use " + this.renderers.TextNames + ", a .NET format string, or date, time, datetime or number for the document's pattern");
                }
            }

            // A loop counter prints as a plain integer unless its tag carries a pattern of its own; a renderer's name is no pattern
            var effective = scope.IsCounter(tag.Path) && (format == null || named) ? this.counterFormatting : this.formatting;
            if (attributes.Count > 0)
            {
                effective = effective.With(attributes);
                if (named)
                {
                    effective = effective.With(NoFormat);
                }
            }

            var text = this.resolver.Format(value, effective);
            if (renderer == null)
            {
                return text;
            }

            try
            {
                return renderer.Render(value, text, effective);
            }
            catch (Exception e)
            {
                this.Report(instruction, FindingKind.RendererFailed, FindingSeverity.Error, tag.Path, "the renderer '" + renderer.Name + "' failed on '" + tag.Path + "' and the value prints blank: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// The text a map gives for a value: the row whose key is the value as the resolver formats it under the library's defaults, else the
        /// map's default row. A map without a row for the key and without a default lets the value print as it is, reported once per tag and key;
        /// so does a name that is no map of the template, reported once per tag.
        /// </summary>
        private string Map(Instruction instruction, string name, object value, Scope scope)
        {
            var tag = instruction.Tag;
            if (!this.maps.TryGetValue(name, out var map))
            {
                this.Report(instruction, FindingKind.UnknownMap, FindingSeverity.Error, tag.Path, "no map '" + name + "' in the template: a map is a table whose first cell holds <@map " + name + ">");
                return this.Format(instruction, value, scope);
            }

            var key = this.resolver.Format(value, Formatting.Default);
            if (map.TryGet(key, out var text))
            {
                return text;
            }

            this.Report(instruction, FindingKind.MissingKey, FindingSeverity.Warning, tag.Path, "the map '" + name + "' has no row for '" + key + "' and no default row: the value prints as it is", key);
            return this.Format(instruction, value, scope);
        }

        /// <summary>
        /// A condition writes the first branch whose test holds: what sits before the first else when its own test holds, else the first else
        /// whose test holds, an else without a test holding always. Nothing when no branch holds.
        /// </summary>
        private void WriteCondition(Instruction instruction, XmlElement element, Scope scope)
        {
            if (this.Holds(instruction, scope))
            {
                for (var child = element.FirstChild; child != null && !IsBranch(child); child = child.NextSibling)
                {
                    this.Write(child, scope);
                }

                return;
            }

            for (var child = element.FirstChild; child != null; child = child.NextSibling)
            {
                if (IsBranch(child) && this.program.Instructions.TryGetValue((XmlElement)child, out var branch) && (!branch.Tag.HasTest || this.Holds(branch, scope)))
                {
                    this.WriteChildren((XmlElement)child, scope);
                    return;
                }
            }
        }

        /// <summary>Whether a test holds: the member exists for an ifdef, the value is true for an if; the other way round after not.</summary>
        private bool Holds(Instruction instruction, Scope scope)
        {
            var tag = instruction.Tag;
            bool outcome;
            if (tag.Defined)
            {
                outcome = this.IsDefined(instruction, scope);
            }
            else
            {
                this.TryResolve(instruction, tag.Path, scope, out var condition);
                outcome = this.resolver.IsTrue(condition);
            }

            return outcome != tag.Negated;
        }

        /// <summary>
        /// A bound frame shows the picture at its path: the bytes go into the package once per distinct content and the frame's image links to
        /// them, with the media type the bytes say; the frame takes the size the tag's fit gives it for the picture's proportions. Without a picture
        /// the frame leaves the document, unless its tag says empty="keep", in which case it keeps the template's picture as the author drew it.
        /// The description that held the tag leaves the output; the title and the rest of the frame stay.
        /// </summary>
        private void WritePicture(Instruction instruction, XmlElement frame, Scope scope)
        {
            var stored = this.TryStorePicture(instruction, frame, scope, out var href, out var format, out var bytes);
            if (!stored && !(instruction.Tag.Attributes.TryGetValue(TagSyntax.EmptyAttribute, out var empty) && TagSyntax.KeepsWhenEmpty(empty)))
            {
                return;
            }

            string width = null;
            string height = null;
            if (stored)
            {
                this.Fit(instruction, frame, format, bytes, out width, out height);
            }

            this.writer.WriteStartElement(frame.Prefix, frame.LocalName, frame.NamespaceURI);
            this.WriteAttributes(frame, true, width, height);

            var image = stored ? frame.ChildNodes.Cast<XmlNode>().FirstOrDefault(v => Is(v, DrawingNamespace, "image")) : null;
            for (var child = frame.FirstChild; child != null; child = child.NextSibling)
            {
                if (child == image)
                {
                    this.WriteImage((XmlElement)child, href, format.MediaType, scope);
                }
                else if (!Is(child, SvgNamespace, "desc"))
                {
                    this.Write(child, scope);
                }
            }

            this.writer.WriteFullEndElement();
        }

        /// <summary>
        /// Stores the picture at the tag's path and gives the name of its file in the package, its format and its bytes; false when there is none: a null
        /// value gives none in silence, and so does a picture renderer that gives null; an unknown member, a value that is no picture, bytes in no format the
        /// library knows and a renderer that fails give none and are reported once per tag. The bytes come from the picture renderer the tag's format names,
        /// else from the resolver; the processor, when the settings have one, gives the bytes the package stores.
        /// </summary>
        private bool TryStorePicture(Instruction instruction, XmlElement frame, Scope scope, out string href, out PictureFormat format, out byte[] bytes)
        {
            href = null;
            format = null;
            bytes = null;

            var path = instruction.Tag.Path;
            if (!this.TryResolve(instruction, path, scope, out var value) || value == null)
            {
                return false;
            }

            var geometry = Geometry(frame, instruction.Tag);
            var renderer = this.PictureRendererOf(instruction);
            if (renderer != null)
            {
                try
                {
                    bytes = renderer.Render(value, geometry);
                }
                catch (Exception e)
                {
                    this.Report(instruction, FindingKind.RendererFailed, FindingSeverity.Error, path, "the renderer '" + renderer.Name + "' failed on '" + path + "' and the frame has no picture: " + e.Message);
                    return false;
                }

                if (bytes == null)
                {
                    return false;
                }

                format = PictureFormat.Sniff(bytes);
                if (format == null)
                {
                    this.Report(instruction, FindingKind.RendererFailed, FindingSeverity.Error, path, "the renderer '" + renderer.Name + "' gave bytes in no picture format the library knows for '" + path + "' and the frame has no picture");
                    return false;
                }
            }
            else
            {
                bytes = this.resolver.Picture(value);
                if (bytes == null)
                {
                    this.Report(instruction, FindingKind.NotAPicture, FindingSeverity.Error, path, "'" + path + "' is not a picture: it is a " + value.GetType().Name);
                    return false;
                }

                format = PictureFormat.Sniff(bytes);
                if (format == null)
                {
                    this.Report(instruction, FindingKind.NotAPicture, FindingSeverity.Error, path, "the bytes at '" + path + "' are in no picture format the library knows: use " + PictureFormat.Names);
                    return false;
                }
            }

            if (this.processor != null)
            {
                this.Process(instruction, geometry, ref bytes, ref format);
            }

            href = this.pictures.Add(path, bytes, format);
            return true;
        }

        /// <summary>
        /// The picture renderer the tag's format names; null when the tag names none. A name that is no picture renderer is reported once per
        /// tag, and the frame shows the resolver's picture for the value instead.
        /// </summary>
        private IPictureRenderer PictureRendererOf(Instruction instruction)
        {
            var tag = instruction.Tag;
            if (!tag.Attributes.TryGetValue(Formatting.FormatAttribute, out var format))
            {
                return null;
            }

            var use = this.renderers.PictureNames.Length == 0 ? "none is registered" : "use " + this.renderers.PictureNames;
            if (!this.renderers.TryGet(format, out var renderer))
            {
                this.Report(instruction, FindingKind.BadValue, FindingSeverity.Error, tag.Path, Formatting.FormatAttribute + "=\"" + format + "\" names no picture renderer: " + use);
                return null;
            }

            var picture = renderer as IPictureRenderer;
            if (picture == null)
            {
                this.Report(instruction, FindingKind.BadValue, FindingSeverity.Error, tag.Path, Formatting.FormatAttribute + "=\"" + format + "\" renders text, not a picture: " + use);
            }

            return picture;
        }

        /// <summary>
        /// The bytes the processor gives for the picture take the place of the bytes it got, under the format they are in; the same bytes or null
        /// leave them as they are. A processor that throws or gives bytes in no format the library knows is reported once per tag, and the picture is stored as it was.
        /// </summary>
        private void Process(Instruction instruction, PictureFrame frame, ref byte[] bytes, ref PictureFormat format)
        {
            var path = instruction.Tag.Path;
            byte[] processed;
            try
            {
                processed = this.processor.Process(bytes, format, frame);
            }
            catch (Exception e)
            {
                this.Report(instruction, FindingKind.RendererFailed, FindingSeverity.Warning, path, "the picture processor failed on '" + path + "' and the picture is stored as it was: " + e.Message);
                return;
            }

            if (processed == null || ReferenceEquals(processed, bytes))
            {
                return;
            }

            var processedFormat = PictureFormat.Sniff(processed);
            if (processedFormat == null)
            {
                this.Report(instruction, FindingKind.RendererFailed, FindingSeverity.Warning, path, "the picture processor gave bytes in no picture format the library knows for '" + path + "' and the picture is stored as it was");
                return;
            }

            bytes = processed;
            format = processedFormat;
        }

        /// <summary>The frame as the author drew it, for a renderer or the processor: its size in inches, 0 for a side without an absolute length, and the fit its tag asks.</summary>
        private static PictureFrame Geometry(XmlElement frame, Tag tag) =>
            new PictureFrame(tag.Path, Inches(frame.GetAttribute("width", SvgNamespace)), Inches(frame.GetAttribute("height", SvgNamespace)), TagSyntax.FitOf(tag));

        private static double Inches(string length) => Length.TryParse(length, null, out var value, out var unit) && value > 0 ? Length.Convert(value, unit, "in") : 0;

        /// <summary>
        /// The frame's size under the tag's fit, for the picture's proportions: null for a side that stays. A picture whose bytes do not tell its size,
        /// and a frame without a width and a height, are reported once per tag, and the picture fills the frame as the author drew it.
        /// </summary>
        private void Fit(Instruction instruction, XmlElement frame, PictureFormat format, byte[] bytes, out string width, out string height)
        {
            width = null;
            height = null;

            var fit = TagSyntax.FitOf(instruction.Tag);
            if (fit == PictureFit.Fill)
            {
                return;
            }

            var path = instruction.Tag.Path;
            if (!PictureSize.TryRead(format, bytes, out var pictureWidth, out var pictureHeight))
            {
                this.Report(instruction, FindingKind.CannotFit, FindingSeverity.Warning, path, "the picture at '" + path + "' fills its frame instead of fitting it: its " + format.Name + " bytes do not tell its size");
            }
            else if (!Fitting.TryFit(fit, frame.GetAttribute("width", SvgNamespace), frame.GetAttribute("height", SvgNamespace), pictureWidth, pictureHeight, out width, out height))
            {
                this.Report(instruction, FindingKind.CannotFit, FindingSeverity.Warning, path, "the picture at '" + path + "' fills its frame instead of fitting it: the frame has no width and height");
            }
        }

        /// <summary>The image links to the stored picture, with its media type, in place of the template's; a picture embedded in the element leaves with it.</summary>
        private void WriteImage(XmlElement image, string href, string mediaType, Scope scope)
        {
            this.writer.WriteStartElement(image.Prefix, image.LocalName, image.NamespaceURI);

            var linked = false;
            foreach (XmlAttribute attribute in image.Attributes)
            {
                var value = attribute.Value;
                if (IsHref(attribute))
                {
                    value = href;
                    linked = true;
                }
                else if (attribute.LocalName == "mime-type" && (attribute.NamespaceURI == DrawingNamespace || attribute.NamespaceURI == LoExtNamespace))
                {
                    value = mediaType;
                }

                this.writer.WriteAttributeString(attribute.Prefix, attribute.LocalName, attribute.NamespaceURI, value);
            }

            if (!linked)
            {
                this.writer.WriteAttributeString("xlink", "href", XLinkNamespace, href);
            }

            this.hrefs.Add(href);

            for (var child = image.FirstChild; child != null; child = child.NextSibling)
            {
                if (!Is(child, OfficeNamespace, "binary-data"))
                {
                    this.Write(child, scope);
                }
            }

            this.writer.WriteFullEndElement();
        }

        /// <summary>The name as given when no frame written so far has it; else the name with the first free number from 2 after it.</summary>
        private string UniqueFrameName(string name)
        {
            if (string.IsNullOrEmpty(name) || this.frameNames.Add(name))
            {
                return name;
            }

            for (var number = 2; ; number++)
            {
                var candidate = name + " " + number;
                if (this.frameNames.Add(candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// The body repeats per item, with the loop variable and the counter in scope; a path that breaks loops over nothing. The separator
        /// is written between two items of a loop that sits in one paragraph; a loop in paragraphs or rows of its own writes none.
        /// </summary>
        private void WriteLoop(Instruction instruction, XmlElement element, Scope scope)
        {
            var tag = instruction.Tag;
            this.TryResolve(instruction, tag.Path, scope, out var collection);
            var items = collection == null ? new List<object>() : this.resolver.Items(collection).ToList();
            if (tag.Sort.Count > 0 && items.Count > 1)
            {
                items = this.Sort(instruction, items);
            }

            var separator = instruction.Inline ? tag.Separator : null;
            var counter = 0;
            foreach (var item in items)
            {
                counter++;
                if (separator != null && counter > 1)
                {
                    ValueWriter.Write(this.writer, separator);
                }

                var body = scope.Push(tag.Variable, item);
                if (tag.Index != null)
                {
                    body = body.Push(tag.Index, counter, counter: true);
                }

                this.WriteChildren(element, body);
            }
        }

        /// <summary>A stable sort by the loop's keys; when two key values cannot be ordered the items keep their order and the loop is reported once.</summary>
        private List<object> Sort(Instruction instruction, List<object> items)
        {
            var keys = instruction.Tag.Sort;
            var keyed = items.Select(item => new Keyed(item, keys.Select(key => this.ResolveKey(instruction, item, key.Path)).ToArray())).ToList();

            try
            {
                return keyed.OrderBy(v => v, new KeyComparer(this.resolver, this.formatting, keys)).Select(v => v.Item).ToList();
            }
            catch (Exception e) when (e is ArgumentException || e.InnerException is ArgumentException)
            {
                var cause = (e as ArgumentException ?? e.InnerException).Message;
                this.Report(instruction, FindingKind.CannotSort, FindingSeverity.Warning, instruction.Tag.Path, "the items keep the collection's order, they cannot be ordered by sort=\"" + string.Join(", ", keys) + "\": " + cause);
                return items;
            }
        }

        /// <summary>
        /// Walks a path: its first name is a loop variable or counter in scope, innermost first, else a member of the root; the rest are members.
        /// False when the path breaks: at a null before its end, which is nothing to report, or at a member the model does not have, reported once per tag.
        /// </summary>
        private bool TryResolve(Instruction instruction, string path, Scope scope, out object value)
        {
            var segments = path.Split('.');
            var start = 0;
            if (scope.TryGetVariable(segments[0], out value))
            {
                start = 1;
            }
            else
            {
                value = scope.Root;
            }

            for (var i = start; i < segments.Length; i++)
            {
                if (value == null)
                {
                    return false;
                }

                if (!this.resolver.TryGetMember(value, segments[i], out value))
                {
                    this.ReportUnknownMember(instruction, path, segments, i, null);
                    value = null;
                    return false;
                }
            }

            return true;
        }

        /// <summary>A sort key is a path relative to the item.</summary>
        private object ResolveKey(Instruction instruction, object item, string path)
        {
            var segments = path.Split('.');
            var value = item;
            for (var i = 0; i < segments.Length; i++)
            {
                if (value == null)
                {
                    return null;
                }

                if (!this.resolver.TryGetMember(value, segments[i], out value))
                {
                    this.ReportUnknownMember(instruction, path, segments, i, instruction.Tag.Variable);
                    return null;
                }
            }

            return value;
        }

        /// <summary>
        /// An ifdef keeps its block when the object at hand has the member, whatever its value: the last name of the path exists on what the
        /// rest of the path reaches. A loop variable or counter is defined. A null before the end is false, without a finding.
        /// </summary>
        private bool IsDefined(Instruction instruction, Scope scope)
        {
            var path = instruction.Tag.Path;
            var dot = path.LastIndexOf('.');
            object holder;
            if (dot < 0)
            {
                if (scope.TryGetVariable(path, out _))
                {
                    return true;
                }

                holder = scope.Root;
            }
            else if (!this.TryResolve(instruction, path.Substring(0, dot), scope, out holder) || holder == null)
            {
                return false;
            }

            return this.resolver.TryGetMember(holder, path.Substring(dot + 1), out _);
        }

        private void ReportUnknownMember(Instruction instruction, string path, string[] segments, int index, string variable)
        {
            var prefix = variable == null ? string.Empty : variable + ".";
            var holder = index == 0 ? variable ?? "the model" : prefix + string.Join(".", segments, 0, index);
            this.Report(instruction, FindingKind.UnknownMember, FindingSeverity.Error, path, holder + " has no member " + segments[index]);
        }

        /// <summary>Adds a finding for the tag, once per tag, kind and path in a render, or once per tag, kind and key when a key is given.</summary>
        private void Report(Instruction instruction, FindingKind kind, FindingSeverity severity, string path, string message, string key = null)
        {
            if (this.reported.Add((instruction, kind, key ?? path)))
            {
                this.findings.Add(new Finding(kind, severity, instruction.Tag.Text, path, message, instruction.Location));
            }
        }

        private sealed class Keyed
        {
            public Keyed(object item, object[] keys)
            {
                this.Item = item;
                this.Keys = keys;
            }

            public object Item { get; }

            public object[] Keys { get; }
        }

        private sealed class KeyComparer : IComparer<Keyed>
        {
            private readonly IResolver resolver;
            private readonly Formatting formatting;
            private readonly IReadOnlyList<SortKey> keys;

            public KeyComparer(IResolver resolver, Formatting formatting, IReadOnlyList<SortKey> keys)
            {
                this.resolver = resolver;
                this.formatting = formatting;
                this.keys = keys;
            }

            public int Compare(Keyed x, Keyed y)
            {
                for (var i = 0; i < this.keys.Count; i++)
                {
                    var order = this.resolver.Compare(x.Keys[i], y.Keys[i], this.formatting);
                    if (order != 0)
                    {
                        return this.keys[i].Descending ? -order : order;
                    }
                }

                return 0;
            }
        }
    }
}
