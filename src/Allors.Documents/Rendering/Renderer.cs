// <copyright file="Renderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Rendering
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using Allors.Document.Models;
    using Allors.Document.Tags;
    using Allors.Document.Validation;
    using Formatting = Allors.Document.Models.Formatting;

    /// <summary>
    /// Renders programs for one model: walks a program's DOM and writes it as XML, with every binding replaced by its formatted value,
    /// every condition kept or dropped and every loop repeated, and reports what the template asks that the model cannot give.
    /// One renderer serves one render; a program is shared between renders and never changed.
    /// The output reads as the template did: its declaration, no byte order mark, a full end tag on every element.
    /// </summary>
    internal sealed class Renderer
    {
        private static readonly byte[] Declaration = Encoding.ASCII.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");

        private static readonly XmlWriterSettings WriterSettings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            NewLineHandling = NewLineHandling.None,
            CloseOutput = false,
        };

        private readonly IResolver resolver;
        private readonly Formatting formatting;
        private readonly Formatting counterFormatting;
        private readonly object model;
        private readonly List<Finding> findings = new List<Finding>();
        private readonly HashSet<(Instruction, string)> reported = new HashSet<(Instruction, string)>();

        private Program program;
        private XmlWriter writer;

        public Renderer(IResolver resolver, Formatting formatting, object model)
        {
            this.resolver = resolver;
            this.formatting = formatting;
            this.model = model;

            // The number pattern is for the model's amounts; a loop counter prints as a plain integer unless its tag says otherwise
            this.counterFormatting = formatting.With(new[] { new KeyValuePair<string, string>(Formatting.NumberSetting, null) });
        }

        /// <summary>Every finding of the parts rendered so far, in render order; a member or a loop is reported once per tag.</summary>
        public IReadOnlyList<Finding> Findings => this.findings;

        /// <summary>The part as the document holds it, with every instruction executed for the model.</summary>
        public byte[] Render(Program program)
        {
            this.program = program;
            this.findings.AddRange(program.Findings);

            using (var output = new MemoryStream())
            {
                output.Write(Declaration, 0, Declaration.Length);
                using (var xmlWriter = XmlWriter.Create(output, WriterSettings))
                {
                    this.writer = xmlWriter;
                    this.Write(program.Document.DocumentElement, new Scope(this.model));
                }

                return output.ToArray();
            }
        }

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

            if (element.HasAttributes)
            {
                foreach (XmlAttribute attribute in element.Attributes)
                {
                    this.writer.WriteAttributeString(attribute.Prefix, attribute.LocalName, attribute.NamespaceURI, attribute.Value);
                }
            }

            this.WriteChildren(element, scope);
            this.writer.WriteFullEndElement();
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

                case TagKind.If:
                    this.TryResolve(instruction, instruction.Tag.Path, scope, out var condition);
                    if (this.resolver.IsTrue(condition))
                    {
                        this.WriteChildren(element, scope);
                    }

                    break;

                case TagKind.IfDef:
                    if (this.IsDefined(instruction, scope))
                    {
                        this.WriteChildren(element, scope);
                    }

                    break;

                case TagKind.For:
                    this.WriteLoop(instruction, element, scope);
                    break;
            }
        }

        /// <summary>
        /// A value prints as the resolver formats it under the tag's formatting; a path that breaks prints nothing.
        /// A loop counter prints as a plain integer, unless the tag carries a format of its own.
        /// </summary>
        private void WriteBinding(Instruction instruction, Scope scope)
        {
            var tag = instruction.Tag;
            if (!this.TryResolve(instruction, tag.Path, scope, out var value))
            {
                return;
            }

            var attributes = tag.Attributes;
            var effective = scope.IsCounter(tag.Path) && !attributes.ContainsKey(Formatting.FormatAttribute) ? this.counterFormatting : this.formatting;
            if (attributes.Count > 0)
            {
                effective = effective.With(attributes);
            }

            ValueWriter.Write(this.writer, this.resolver.Format(value, effective));
        }

        /// <summary>The body repeats per item, with the loop variable and the counter in scope; a path that breaks loops over nothing.</summary>
        private void WriteLoop(Instruction instruction, XmlElement element, Scope scope)
        {
            var tag = instruction.Tag;
            this.TryResolve(instruction, tag.Path, scope, out var collection);
            var items = collection == null ? new List<object>() : this.resolver.Items(collection).ToList();
            if (tag.Sort.Count > 0 && items.Count > 1)
            {
                items = this.Sort(instruction, items);
            }

            var counter = 0;
            foreach (var item in items)
            {
                counter++;
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
                if (this.reported.Add((instruction, instruction.Tag.Path)))
                {
                    this.findings.Add(new Finding(FindingKind.CannotSort, FindingSeverity.Warning, instruction.Tag.Text, instruction.Tag.Path, "the items keep the collection's order, they cannot be ordered by sort=\"" + string.Join(", ", keys) + "\": " + cause, instruction.Location));
                }

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
            if (!this.reported.Add((instruction, path)))
            {
                return;
            }

            var prefix = variable == null ? string.Empty : variable + ".";
            var holder = index == 0 ? variable ?? "the model" : prefix + string.Join(".", segments, 0, index);
            this.findings.Add(new Finding(FindingKind.UnknownMember, FindingSeverity.Error, instruction.Tag.Text, path, holder + " has no member " + segments[index], instruction.Location));
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
