// <copyright file="TagScanner.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Templating;

using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

/// <summary>The tags found by scanning a range of nodes.</summary>
internal sealed class ScanResult
{
    internal ScanResult(BlockPair root, List<BindingTag> bindings, List<FrameTag> frames)
    {
        this.Root = root;
        this.Bindings = bindings;
        this.Frames = frames;
    }

    /// <summary>The root pair; its children are the top level blocks of the scanned range.</summary>
    internal BlockPair Root { get; }

    /// <summary>The bindings of the scanned range that are not inside any block.</summary>
    internal IReadOnlyList<BindingTag> Bindings { get; }

    /// <summary>The template frames of the scanned range that are not inside any block.</summary>
    internal IReadOnlyList<FrameTag> Frames { get; }
}

/// <summary>
/// Scans a range of sibling nodes (and their descendants, in document order) for template tags,
/// matching begin/end pairs into a <see cref="BlockPair"/> tree. Bindings and frames inside a
/// block are not collected: block content is rescanned when the block itself is rendered.
/// </summary>
internal static class TagScanner
{
    internal static ScanResult Scan(IEnumerable<XNode> nodes, string? source = null)
    {
        var root = new BlockPair(null, null);
        var current = root;
        var stack = new Stack<BlockPair>();
        var bindings = new List<BindingTag>();
        var frames = new List<FrameTag>();

        foreach (var node in nodes)
        {
            if (node is not XElement element)
            {
                continue;
            }

            foreach (var descendant in element.DescendantsAndSelf())
            {
                if (TagSyntax.IsPlaceholder(descendant))
                {
                    var tag = CreateTag(descendant, source);

                    switch (tag)
                    {
                        case BindingTag binding:
                            if (stack.Count == 0)
                            {
                                bindings.Add(binding);
                            }

                            break;

                        case ForTag or IfTag:
                            var child = new BlockPair(current, tag);
                            current.AddChild(child);
                            stack.Push(child);
                            current = child;
                            break;

                        case EndTag:
                            if (stack.Count == 0)
                            {
                                throw new TemplateException(new[]
                                {
                                    new TemplateError("Unbalanced '<@end>': there is no open '<@for>' or '<@if>'.", source, descendant.Value.Trim()),
                                });
                            }

                            var closed = stack.Pop();
                            closed.Close(descendant);
                            current = closed.Parent!;
                            break;
                    }
                }
                else if (stack.Count == 0)
                {
                    var frame = TagSyntax.TryCreateFrame(descendant);
                    if (frame is not null)
                    {
                        frames.Add(frame);
                    }
                }
            }
        }

        if (stack.Count > 0)
        {
            var open = stack.Peek();
            var openText = open.Begin!.Value.Trim();
            throw new TemplateException(new[]
            {
                new TemplateError($"Missing '<@end>' for '{openText}'.", source, openText),
            });
        }

        return new ScanResult(root, bindings, frames);
    }

    private static Tag? CreateTag(XElement placeholder, string? source)
    {
        try
        {
            return TagSyntax.TryCreate(placeholder);
        }
        catch (TemplateException exception)
        {
            throw new TemplateException(exception.Errors
                .Select(error => new TemplateError(error.Message, error.Source ?? source, error.Tag ?? placeholder.Value.Trim()))
                .ToList());
        }
    }
}
