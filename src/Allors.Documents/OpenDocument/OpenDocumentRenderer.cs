// <copyright file="OpenDocumentRenderer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.OpenDocument;

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Allors.Documents.Expressions;
using Allors.Documents.Templating;

/// <summary>
/// Renders template tags directly on the XML DOM, in a single mutating pass:
/// bindings become text nodes, false conditionals are removed, loops deep-clone
/// their block once per item and frame name expressions are evaluated in scope.
/// Block content is rescanned when it is rendered, so clones never share tag
/// state with their prototype.
/// </summary>
internal sealed class OpenDocumentRenderer
{
    private readonly string source;

    internal OpenDocumentRenderer(string source) => this.source = source;

    internal void Render(XDocument document, RenderScope scope)
    {
        var root = document.Root
            ?? throw new TemplateException(new[] { new TemplateError("The document has no root element.", this.source) });

        this.RenderRange(new XNode[] { root }, scope);
    }

    private void RenderRange(IEnumerable<XNode> nodes, RenderScope scope)
    {
        var scan = TagScanner.Scan(nodes, this.source);

        foreach (var binding in scan.Bindings)
        {
            ReplaceBinding(binding, scope);
        }

        foreach (var frame in scan.Frames)
        {
            EvaluateFrameName(frame, scope);
        }

        foreach (var pair in scan.Root.Children)
        {
            this.RenderBlock(pair, scope);
        }
    }

    private void RenderBlock(BlockPair pair, RenderScope scope)
    {
        switch (pair.Statement)
        {
            case IfTag ifTag:
                this.RenderIf(pair, ifTag, scope);
                break;

            case ForTag forTag:
                this.RenderFor(pair, forTag, scope);
                break;
        }
    }

    private void RenderIf(BlockPair pair, IfTag ifTag, RenderScope scope)
    {
        var content = pair.ContentNodes();

        if (Truthiness.IsTruthy(ifTag.Condition.Evaluate(scope)))
        {
            this.RenderRange(content, scope);
        }
        else
        {
            foreach (var node in content)
            {
                node.Remove();
            }
        }

        pair.BeginSibling!.Remove();
        pair.EndSibling!.Remove();
    }

    private void RenderFor(BlockPair pair, ForTag forTag, RenderScope scope)
    {
        var items = ToItems(forTag.Collection.Evaluate(scope));
        var prototype = pair.ContentNodes();
        var beginSibling = pair.BeginSibling!;

        var index = 1;
        foreach (var item in items)
        {
            var childScope = scope.CreateChild();
            childScope.Set(forTag.ItemName, item);
            childScope.Set("i", index);
            childScope.Set("i0", index - 1);

            // Adding parented nodes to a new element clones them.
            var container = new XElement("container", prototype);
            this.RenderRange(container.Nodes().ToList(), childScope);

            var rendered = container.Nodes().ToList();
            container.RemoveNodes();
            beginSibling.AddBeforeSelf(rendered);

            index++;
        }

        foreach (var node in prototype)
        {
            node.Remove();
        }

        beginSibling.Remove();
        pair.EndSibling!.Remove();
    }

    private static void ReplaceBinding(BindingTag binding, RenderScope scope)
    {
        var text = ToText(binding.Expression.Evaluate(scope));

        if (text.Length == 0)
        {
            binding.Element.Remove();
        }
        else
        {
            binding.Element.ReplaceWith(new XText(text));
        }
    }

    private static void EvaluateFrameName(FrameTag frame, RenderScope scope) =>
        frame.NameAttribute.Value = ToText(frame.NameExpression.Evaluate(scope));

    private static string ToText(object? value) =>
        XmlText.Sanitize(value switch
        {
            null => null,
            string s => s,
            System.IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        });

    private static List<object?> ToItems(object? value)
    {
        switch (value)
        {
            case null:
                return new List<object?>();

            case string s:
                return new List<object?> { s };

            case IEnumerable enumerable:
            {
                var items = new List<object?>();
                foreach (var item in enumerable)
                {
                    items.Add(item);
                }

                return items;
            }

            default:
                return new List<object?> { value };
        }
    }
}
