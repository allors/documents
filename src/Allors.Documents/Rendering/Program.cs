// <copyright file="Program.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Rendering
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using Allors.Document.Inspection;
    using Allors.Document.Tags;
    using Allors.Document.Validation;
    using Allors.Document.Xml;

    /// <summary>
    /// A part of a template (content.xml or styles.xml) ready to render: its DOM with every tag replaced by an instruction element in the
    /// program's own namespace, a binding where its placeholder stood and a block around what sits between its two tags, with the paragraphs
    /// or rows that held the block tags gone; a bound picture frame stays where it is and is an instruction itself. Built once per template
    /// and never changed; every render walks it.
    /// A block that never ends, an end that opens nothing, or a block that would cut a table throws <see cref="TemplateException"/>.
    /// </summary>
    internal sealed class Program
    {
        /// <summary>The namespace of the instruction elements; it never reaches the output.</summary>
        public const string Namespace = "urn:allors:documents:program";

        private const string Prefix = "ad";

        private Program(string part, XmlDocument document, IReadOnlyDictionary<XmlElement, Instruction> instructions, IReadOnlyList<Finding> findings)
        {
            this.Part = part;
            this.Document = document;
            this.Instructions = instructions;
            this.Findings = findings;
        }

        public string Part { get; }

        public XmlDocument Document { get; }

        /// <summary>The instruction behind each element in the program's namespace.</summary>
        public IReadOnlyDictionary<XmlElement, Instruction> Instructions { get; }

        /// <summary>Tags that cannot be read and render as the text the author typed; every render reports them.</summary>
        public IReadOnlyList<Finding> Findings { get; }

        public static Program Build(string part, byte[] xml)
        {
            var document = Inspector.Load(xml);
            var tags = new List<InspectedTag>();
            var pictures = new List<InspectedPicture>();
            var problems = new List<InspectionProblem>();
            new Inspector.PartWalker(part, document, tags, pictures, problems).Walk();

            var unbalanced = problems.Where(v => v.IsUnbalanced).Select(v => new Finding(FindingKind.UnbalancedBlock, FindingSeverity.Error, v.Text, null, v.Message, v.Location)).ToList();
            if (unbalanced.Count > 0)
            {
                throw new TemplateException(unbalanced);
            }

            var malformed = problems.Select(v => new Finding(FindingKind.MalformedTag, FindingSeverity.Error, v.Text, null, v.Message, v.Location)).ToList();

            var instructions = new Dictionary<XmlElement, Instruction>();
            var root = new XmlPair(null, null);
            var tagByPair = new Dictionary<XmlPair, InspectedTag>();
            var misplaced = new List<Finding>();
            Prepare(tags, root, document, instructions, tagByPair, misplaced);
            if (misplaced.Count > 0)
            {
                throw new TemplateException(misplaced);
            }

            foreach (var picture in pictures)
            {
                instructions.Add(picture.Element, new Instruction(picture.Tag, picture.Location));
            }

            root.Rewrite(pair =>
            {
                var tag = tagByPair[pair];
                var element = document.CreateElement(Prefix, Name(tag.Tag.Kind), Namespace);
                instructions.Add(element, new Instruction(tag.Tag, tag.Location));
                return element;
            });

            return new Program(part, document, instructions, malformed);
        }

        /// <summary>Replaces every binding placeholder by its instruction element and pairs every block's begin and end for the rewrite.</summary>
        private static void Prepare(IEnumerable<InspectedTag> tags, XmlPair parent, XmlDocument document, Dictionary<XmlElement, Instruction> instructions, Dictionary<XmlPair, InspectedTag> tagByPair, List<Finding> misplaced)
        {
            foreach (var tag in tags)
            {
                switch (tag.Tag.Kind)
                {
                    case TagKind.Binding:
                        var binding = document.CreateElement(Prefix, Name(TagKind.Binding), Namespace);
                        tag.Element.ParentNode.ReplaceChild(binding, tag.Element);
                        instructions.Add(binding, new Instruction(tag.Tag, tag.Location));
                        break;

                    case TagKind.For:
                    case TagKind.If:
                    case TagKind.IfDef:
                        var pair = new XmlPair(parent, tag.Element) { End = tag.End.Element };
                        parent.Children.Add(pair);
                        tagByPair.Add(pair, tag);

                        var problem = BlockStructure.Problem(pair);
                        if (problem != null)
                        {
                            misplaced.Add(new Finding(FindingKind.BlockStructure, FindingSeverity.Error, tag.Tag.Text, null, BlockStructure.Message(problem), tag.Location));
                        }

                        Prepare(tag.Children, pair, document, instructions, tagByPair, misplaced);
                        break;
                }
            }
        }

        private static string Name(TagKind kind)
        {
            switch (kind)
            {
                case TagKind.Binding:
                    return "binding";
                case TagKind.For:
                    return "for";
                case TagKind.If:
                    return "if";
                default:
                    return "ifdef";
            }
        }
    }
}
