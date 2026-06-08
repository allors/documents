// <copyright file="Tag.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Templating;

using System.Xml.Linq;
using Allors.Documents.Expressions;

/// <summary>A template tag found in the document.</summary>
internal abstract class Tag
{
    protected Tag(XElement element) => this.Element = element;

    /// <summary>The element that carries the tag.</summary>
    internal XElement Element { get; }
}

/// <summary>A variable binding: <c>&lt;$Person.FirstName&gt;</c>, optionally with a fallback such as <c>&lt;$Name ?? 'N/A'&gt;</c>.</summary>
internal sealed class BindingTag : Tag
{
    internal BindingTag(XElement element, Expression expression)
        : base(element) =>
        this.Expression = expression;

    internal Expression Expression { get; }
}

/// <summary>A loop: <c>&lt;@for p People&gt;</c>.</summary>
internal sealed class ForTag : Tag
{
    internal ForTag(XElement element, string itemName, PathExpression collection)
        : base(element)
    {
        this.ItemName = itemName;
        this.Collection = collection;
    }

    internal string ItemName { get; }

    internal PathExpression Collection { get; }
}

/// <summary>A conditional: <c>&lt;@if Person.FirstName&gt;</c>.</summary>
internal sealed class IfTag : Tag
{
    internal IfTag(XElement element, Expression condition)
        : base(element) =>
        this.Condition = condition;

    internal Expression Condition { get; }
}

/// <summary>The end of the nearest open loop or conditional: <c>&lt;@end&gt;</c>.</summary>
internal sealed class EndTag : Tag
{
    internal EndTag(XElement element)
        : base(element)
    {
    }
}

/// <summary>A drawing frame whose name is a template expression: <c>draw:name="$image"</c>.</summary>
internal sealed class FrameTag : Tag
{
    internal FrameTag(XElement element, XAttribute nameAttribute, PathExpression nameExpression)
        : base(element)
    {
        this.NameAttribute = nameAttribute;
        this.NameExpression = nameExpression;
    }

    internal XAttribute NameAttribute { get; }

    internal PathExpression NameExpression { get; }
}
