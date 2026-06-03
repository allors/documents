// <copyright file="BlockPair.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Templating;

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// A matched begin/end tag pair. <see cref="BeginSibling"/> and <see cref="EndSibling"/> are the
/// children of the pair's lowest common ancestor that contain (or are) the begin and end markers;
/// the pair's block is the run of sibling nodes strictly between them. The sibling nodes that
/// carry the markers belong to the markers and are removed when the block is processed.
/// </summary>
internal sealed class BlockPair
{
    private readonly List<BlockPair> children = new();

    internal BlockPair(BlockPair? parent, Tag? statement)
    {
        this.Parent = parent;
        this.Statement = statement;
    }

    internal BlockPair? Parent { get; }

    /// <summary>The opening tag (<see cref="ForTag"/> or <see cref="IfTag"/>); null for the root pair.</summary>
    internal Tag? Statement { get; }

    internal IReadOnlyList<BlockPair> Children => this.children;

    internal XElement? Begin => this.Statement?.Element;

    internal XElement? End { get; private set; }

    internal XElement? BeginSibling { get; private set; }

    internal XElement? EndSibling { get; private set; }

    internal bool IsRoot => this.Parent is null;

    internal void AddChild(BlockPair child) => this.children.Add(child);

    internal void Close(XElement end)
    {
        this.End = end;
        this.FindSiblings();
    }

    /// <summary>The nodes strictly between <see cref="BeginSibling"/> and <see cref="EndSibling"/>.</summary>
    internal List<XNode> ContentNodes()
    {
        var nodes = new List<XNode>();

        for (var node = this.BeginSibling!.NextNode; node is not null && node != this.EndSibling; node = node.NextNode)
        {
            nodes.Add(node);
        }

        return nodes;
    }

    public override string ToString() => this.Begin?.Value.Trim() ?? "(root)";

    private void FindSiblings()
    {
        var begin = this.Begin!;
        var end = this.End!;

        var beginAncestry = begin.AncestorsAndSelf().ToList();
        var endAncestry = end.AncestorsAndSelf().ToList();
        var endAncestrySet = new HashSet<XElement>(endAncestry);

        XElement? lowestCommonAncestor = null;
        foreach (var ancestor in beginAncestry)
        {
            if (endAncestrySet.Contains(ancestor))
            {
                lowestCommonAncestor = ancestor;
                break;
            }
        }

        if (lowestCommonAncestor is null)
        {
            throw new TemplateException($"Tags '{begin.Value.Trim()}' and '{end.Value.Trim()}' do not share a common ancestor.");
        }

        var beginIndex = beginAncestry.IndexOf(lowestCommonAncestor);
        var endIndex = endAncestry.IndexOf(lowestCommonAncestor);

        if (beginIndex == 0 || endIndex == 0)
        {
            throw new TemplateException($"Tags '{begin.Value.Trim()}' and '{end.Value.Trim()}' are nested inside each other.");
        }

        this.BeginSibling = beginAncestry[beginIndex - 1];
        this.EndSibling = endAncestry[endIndex - 1];
    }
}
