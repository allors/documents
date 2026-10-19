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
    /// branch element for what follows each separator. The root pair has no begin: it holds the blocks at the top level of the part.
    /// </summary>
    internal class XmlPair
    {
        /// <summary>The branch factory of a block without separators, which the rewrite never asks for a branch.</summary>
        private static readonly Func<XmlPair, int, XmlElement> NoBranches = (pair, index) => throw new InvalidOperationException("a block without separators has no branches");

        private readonly List<XmlElement> separators = new List<XmlElement>();
        private readonly List<XmlElement?> separatorSiblings = new List<XmlElement?>();
        private XmlElement? end;

        public XmlPair(XmlPair? parent, XmlElement? begin)
        {
            this.Parent = parent;
            this.Children = new List<XmlPair>();
            this.Begin = begin;
        }

        public XmlPair? Parent { get; }

        public XmlDocument? Document => this.Begin?.OwnerDocument;

        public IList<XmlPair> Children { get; }

        /// <summary>The element that begins the block; null for the root pair.</summary>
        public XmlElement? Begin { get; }

        /// <summary>The element that ends the block; null until it is set, which finds the container and the siblings.</summary>
        public XmlElement? End
        {
            get => this.end;
            set
            {
                this.end = value;
                this.FindSiblings();
            }
        }

        /// <summary>The lowest common ancestor of the begin and the end; null until the end is set.</summary>
        public XmlElement? Container { get; private set; }

        /// <summary>The child of the container that holds the begin; null until the end is set.</summary>
        public XmlElement? BeginSibling { get; private set; }

        /// <summary>The child of the container that holds the end; null until the end is set.</summary>
        public XmlElement? EndSibling { get; private set; }

        /// <summary>The elements that split the block into branches, in document order.</summary>
        public IReadOnlyList<XmlElement> Separators => this.separators;

        /// <summary>The child of the container that holds each separator; null for a separator outside the container.</summary>
        public IReadOnlyList<XmlElement?> SeparatorSiblings => this.separatorSiblings;

        public bool IsRoot => this.Parent == null;

        /// <summary>Adds a separator; the end must be set first.</summary>
        public void AddSeparator(XmlElement separator)
        {
            this.separators.Add(separator);
            this.separatorSiblings.Add(this.SiblingOf(separator));
        }

        /// <summary>Rewrites this block and its children, none of which has separators.</summary>
        public void Rewrite(Func<XmlPair, XmlElement> factory) => this.Rewrite(factory, null, null);

        /// <summary>Rewrites this block and its children; the branch factory makes the element for the branch after each separator, by its index.</summary>
        public void Rewrite(Func<XmlPair, XmlElement> factory, Func<XmlPair, int, XmlElement> branchFactory) => this.Rewrite(factory, branchFactory, null);

        public override string ToString() => this.Begin == null ? "root" : this.Begin.LocalName + ": " + this.Begin.InnerText;

        /// <summary>
        /// Rewrites this block into an instruction element and then its children into theirs. A sibling that also began or ended the parent
        /// block is gone by then, removed by the parent's rewrite: the block then starts at the first child of the parent's instruction, or
        /// ends with its last.
        /// </summary>
        private void Rewrite(Func<XmlPair, XmlElement> factory, Func<XmlPair, int, XmlElement>? branchFactory, XmlElement? parentInstruction)
        {
            XmlElement? instructionElement = null;
            if (!this.IsRoot)
            {
                var branches = branchFactory ?? (this.separators.Count > 0 ? throw new InvalidOperationException("a block with separators needs a branch factory") : NoBranches);
                if (this.BeginSibling == null || this.EndSibling == null)
                {
                    throw new InvalidOperationException("a block needs its end before it is rewritten");
                }

                var beginSibling = this.BeginSibling;
                var endSibling = this.EndSibling;
                var sharedBegin = beginSibling.ParentNode == null;
                var parentNode = (sharedBegin ? parentInstruction : beginSibling.ParentNode) ?? throw new InvalidOperationException("the block's begin has left the document");

                instructionElement = factory(this);
                var target = instructionElement;
                var branch = 0;

                var nextSibling = sharedBegin ? parentNode.FirstChild : beginSibling.NextSibling;
                while (nextSibling != null && nextSibling != endSibling)
                {
                    if (branch < this.separatorSiblings.Count && nextSibling == this.separatorSiblings[branch])
                    {
                        target = branches(this, branch);
                        instructionElement.AppendChild(target);
                        parentNode.RemoveChild(nextSibling);
                        branch++;
                    }
                    else
                    {
                        target.AppendChild(nextSibling);
                    }

                    nextSibling = sharedBegin ? parentNode.FirstChild : beginSibling.NextSibling;
                }

                if (sharedBegin)
                {
                    parentNode.PrependChild(instructionElement);
                }
                else
                {
                    parentNode.InsertBefore(instructionElement, beginSibling);
                    parentNode.RemoveChild(beginSibling);
                }

                if (endSibling.ParentNode == parentNode)
                {
                    parentNode.RemoveChild(endSibling);
                }
            }

            foreach (var child in this.Children)
            {
                child.Rewrite(factory, branchFactory, instructionElement);
            }
        }

        private void FindSiblings()
        {
            this.Container = null;
            this.BeginSibling = null;
            this.EndSibling = null;
            if (this.Begin == null || this.end == null)
            {
                return;
            }

            var beginAncestry = new List<XmlElement> { this.Begin };
            GetAncestry(this.Begin, beginAncestry);

            var endAncestry = new List<XmlElement> { this.end };
            GetAncestry(this.end, endAncestry);

            XmlElement? lowestCommonAncestor = null;
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
        private XmlElement? SiblingOf(XmlElement element)
        {
            if (this.Container == null)
            {
                return null;
            }

            for (XmlElement? node = element; node != null; node = node.ParentNode as XmlElement)
            {
                if (node.ParentNode == this.Container)
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>Adds the element and every element above it, up to the root element.</summary>
        private static void GetAncestry(XmlElement element, List<XmlElement> ancestry)
        {
            for (XmlElement? node = element; node != null; node = node.ParentNode as XmlElement)
            {
                ancestry.Add(node);
            }
        }
    }
}
