// <copyright file="Program.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;

    /// <summary>
    /// A part of a template (content.xml or styles.xml) ready to render: its DOM with every tag replaced by an instruction element in the
    /// program's own namespace, a binding where its placeholder stood and a block around what sits between its two tags, with the paragraphs
    /// or rows that held the block tags gone; a condition holds its first branch, then an else element per further branch; a bound picture
    /// frame stays where it is and is an instruction itself; a map table leaves the part. Built once per template and never changed; every render walks it.
    /// A block that never ends, an end that opens nothing, an else that belongs to no condition, or a block that would cut a table throws <see cref="TemplateException"/>.
    /// </summary>
    internal sealed class Program
    {
        /// <summary>The namespace of the instruction elements; it never reaches the output.</summary>
        public const string Namespace = "urn:allors:documents:program";

        /// <summary>The local name of the element that holds a further branch of a condition.</summary>
        public const string ElseName = "else";

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

        /// <summary>Builds a part's program, or throws <see cref="TemplateException"/> when the part cannot render; see <see cref="TryBuild"/>.</summary>
        public static Program Build(string part, byte[] xml, List<InspectedMap>? maps = null)
        {
            var blocking = new List<Finding>();
            return TryBuild(part, xml, maps, blocking) ?? throw new TemplateException(blocking);
        }

        /// <summary>
        /// Builds a part's program, or gives null and adds to the blocking findings why the part cannot render: a block that never ends, an end
        /// that opens nothing, an else that belongs to no condition, or a block that would cut a table. The maps found in the part are added to
        /// the list, which the parts of one template share so that a name is defined once.
        /// </summary>
        public static Program? TryBuild(string part, byte[] xml, List<InspectedMap>? maps, List<Finding> blocking)
        {
            var document = Inspector.Load(xml);
            var tags = new List<InspectedTag>();
            var pictures = new List<InspectedPicture>();
            var problems = new List<InspectionProblem>();
            maps = maps ?? new List<InspectedMap>();
            new Inspector.PartWalker(part, document, tags, pictures, maps, problems).Walk();

            var unbalanced = problems.Where(v => v.IsUnbalanced).Select(v => new Finding(FindingKind.UnbalancedBlock, FindingSeverity.Error, v.Text, null, v.Message, v.Location)).ToList();
            if (unbalanced.Count > 0)
            {
                blocking.AddRange(unbalanced);
                return null;
            }

            var malformed = problems.Select(v => new Finding(FindingKind.MalformedTag, FindingSeverity.Error, v.Text, null, v.Message, v.Location)).ToList();

            var instructions = new Dictionary<XmlElement, Instruction>();
            var root = new XmlPair(null, null);
            var tagByPair = new Dictionary<XmlPair, InspectedTag>();
            var branchesByPair = new Dictionary<XmlPair, IReadOnlyList<InspectedTag>>();
            var misplaced = new List<Finding>();
            Prepare(tags, root, document, instructions, tagByPair, branchesByPair, misplaced);
            if (misplaced.Count > 0)
            {
                blocking.AddRange(misplaced);
                return null;
            }

            foreach (var picture in pictures)
            {
                instructions.Add(picture.Element, new Instruction(picture.Tag, picture.Location));
            }

            foreach (var map in maps.Where(v => v.Element.OwnerDocument == document))
            {
                map.Element.ParentNode?.RemoveChild(map.Element);
            }

            root.Rewrite(
                pair =>
                {
                    var tag = tagByPair[pair];
                    var element = document.CreateElement(Prefix, Name(tag.Tag.Kind), Namespace);
                    instructions.Add(element, new Instruction(tag.Tag, tag.Location, BlockStructure.IsInline(pair)));
                    return element;
                },
                (pair, index) =>
                {
                    var branch = branchesByPair[pair][index];
                    var element = document.CreateElement(Prefix, ElseName, Namespace);
                    instructions.Add(element, new Instruction(branch.Tag, branch.Location));
                    return element;
                });

            return new Program(part, document, instructions, malformed);
        }

        /// <summary>Replaces every binding placeholder by its instruction element and pairs every block's begin, elses and end for the rewrite.</summary>
        private static void Prepare(IEnumerable<InspectedTag> tags, XmlPair parent, XmlDocument document, Dictionary<XmlElement, Instruction> instructions, Dictionary<XmlPair, InspectedTag> tagByPair, Dictionary<XmlPair, IReadOnlyList<InspectedTag>> branchesByPair, List<Finding> misplaced)
        {
            foreach (var tag in tags)
            {
                switch (tag.Tag.Kind)
                {
                    case TagKind.Binding:
                        var binding = document.CreateElement(Prefix, Name(TagKind.Binding), Namespace);
                        tag.Element.ParentNode?.ReplaceChild(binding, tag.Element);
                        instructions.Add(binding, new Instruction(tag.Tag, tag.Location));
                        break;

                    case TagKind.For:
                    case TagKind.If:
                    case TagKind.IfDef:
                        var pair = new XmlPair(parent, tag.Element) { End = tag.End?.Element };
                        parent.Children.Add(pair);
                        tagByPair.Add(pair, tag);

                        var branches = tag.Elses.ToList();
                        foreach (var branch in branches)
                        {
                            pair.AddSeparator(branch.Element);
                        }

                        branchesByPair.Add(pair, branches);

                        var problem = BlockStructure.Problem(pair);
                        if (problem != null)
                        {
                            misplaced.Add(new Finding(FindingKind.BlockStructure, FindingSeverity.Error, tag.Tag.Text, null, BlockStructure.Message(problem), tag.Location));
                        }

                        Prepare(tag.Children, pair, document, instructions, tagByPair, branchesByPair, misplaced);
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
