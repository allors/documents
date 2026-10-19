// <copyright file="XmlPair.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Xml;

    /// <summary>
    /// A block of a part: the element that begins it, the element that ends it, and the elements between them that split it into branches.
    /// The container is the lowest common ancestor of the begin and the end; the siblings are its children that hold them. The rewrite
    /// replaces everything from the begin sibling to the end sibling by one instruction element holding what sat between them, with a
    /// branch element for what follows each separator.
    /// </summary>
    internal class XmlPair
    {
        private readonly List<XmlElement> separators = new List<XmlElement>();
        private readonly List<XmlElement> separatorSiblings = new List<XmlElement>();
        private XmlElement end;

        public XmlPair(XmlPair parent, XmlElement begin)
        {
            this.Parent = parent;
            this.Children = new List<XmlPair>();
            this.Begin = begin;
        }

        public XmlPair Parent { get; }

        public XmlDocument Document => this.Begin.OwnerDocument;

        public IList<XmlPair> Children { get; }

        public XmlElement Begin { get; }

        public XmlElement End
        {
            get => this.end;
            set
            {
                this.end = value;
                this.FindSiblings();
            }
        }

        /// <summary>The lowest common ancestor of the begin and the end; null until the end is set.</summary>
        public XmlElement Container { get; private set; }

        public XmlElement BeginSibling { get; private set; }

        public XmlElement EndSibling { get; private set; }

        /// <summary>The elements that split the block into branches, in document order.</summary>
        public IReadOnlyList<XmlElement> Separators => this.separators;

        /// <summary>The child of the container that holds each separator; null for a separator outside the container.</summary>
        public IReadOnlyList<XmlElement> SeparatorSiblings => this.separatorSiblings;

        public bool IsRoot => this.Parent == null;

        /// <summary>Adds a separator; the end must be set first.</summary>
        public void AddSeparator(XmlElement separator)
        {
            this.separators.Add(separator);
            this.separatorSiblings.Add(this.SiblingOf(separator));
        }

        public void Rewrite(Func<XmlPair, XmlElement> factory) => this.Rewrite(factory, null);

        /// <summary>Rewrites this block and its children; the branch factory makes the element for the branch after each separator, by its index.</summary>
        public void Rewrite(Func<XmlPair, XmlElement> factory, Func<XmlPair, int, XmlElement> branchFactory) => this.Rewrite(factory, branchFactory, null);

        public override string ToString() => this.Begin.LocalName + ": " + this.Begin.InnerText;

        /// <summary>
        /// Rewrites this block into an instruction element and then its children into theirs. A sibling that also began or ended the parent
        /// block is gone by then, removed by the parent's rewrite: the block then starts at the first child of the parent's instruction, or
        /// ends with its last.
        /// </summary>
        private void Rewrite(Func<XmlPair, XmlElement> factory, Func<XmlPair, int, XmlElement> branchFactory, XmlElement parentInstruction)
        {
            XmlElement instructionElement = null;
            if (!this.IsRoot)
            {
                if (this.separators.Count > 0 && branchFactory == null)
                {
                    throw new InvalidOperationException("a block with separators needs a branch factory");
                }

                var sharedBegin = this.BeginSibling.ParentNode == null;
                var parentNode = sharedBegin ? parentInstruction : this.BeginSibling.ParentNode;

                instructionElement = factory(this);
                var target = instructionElement;
                var branch = 0;

                var nextSibling = sharedBegin ? parentNode.FirstChild : this.BeginSibling.NextSibling;
                while (nextSibling != null && nextSibling != this.EndSibling)
                {
                    if (branch < this.separatorSiblings.Count && nextSibling == this.separatorSiblings[branch])
                    {
                        target = branchFactory(this, branch);
                        instructionElement.AppendChild(target);
                        parentNode.RemoveChild(nextSibling);
                        branch++;
                    }
                    else
                    {
                        target.AppendChild(nextSibling);
                    }

                    nextSibling = sharedBegin ? parentNode.FirstChild : this.BeginSibling.NextSibling;
                }

                if (sharedBegin)
                {
                    parentNode.PrependChild(instructionElement);
                }
                else
                {
                    parentNode.InsertBefore(instructionElement, this.BeginSibling);
                    parentNode.RemoveChild(this.BeginSibling);
                }

                if (this.EndSibling.ParentNode == parentNode)
                {
                    parentNode.RemoveChild(this.EndSibling);
                }
            }

            foreach (var child in this.Children)
            {
                child.Rewrite(factory, branchFactory, instructionElement);
            }
        }

        private void FindSiblings()
        {
            var beginAncestry = new List<XmlElement> { this.Begin };
            this.GetAncestry(this.Begin, beginAncestry);

            var endAncestry = new List<XmlElement> { this.End };
            this.GetAncestry(this.End, endAncestry);

            XmlElement lowestCommonAncestor = null;
            foreach (var ancestor in beginAncestry)
            {
                if (endAncestry.Contains(ancestor))
                {
                    lowestCommonAncestor = ancestor;
                    break;
                }
            }

            this.Container = lowestCommonAncestor;
            if (lowestCommonAncestor != null)
            {
                this.BeginSibling = beginAncestry[beginAncestry.IndexOf(lowestCommonAncestor) - 1];
                this.EndSibling = endAncestry[endAncestry.IndexOf(lowestCommonAncestor) - 1];
            }

            for (var i = 0; i < this.separators.Count; i++)
            {
                this.separatorSiblings[i] = this.SiblingOf(this.separators[i]);
            }
        }

        /// <summary>The child of the container on the way down to the element; null when the element is not inside the container.</summary>
        private XmlElement SiblingOf(XmlElement element)
        {
            if (this.Container == null)
            {
                return null;
            }

            for (var node = element; node != null; node = node.ParentNode as XmlElement)
            {
                if (node.ParentNode == this.Container)
                {
                    return node;
                }
            }

            return null;
        }

        private void GetAncestry(XmlElement element, List<XmlElement> ancestry)
        {
            while (true)
            {
                if (element != null)
                {
                    ancestry.Add(element);
                    if (element.ParentNode is XmlElement node)
                    {
                        element = node;
                    }
                    else
                    {
                        element = null;
                    }

                    continue;
                }

                break;
            }
        }
    }
}
