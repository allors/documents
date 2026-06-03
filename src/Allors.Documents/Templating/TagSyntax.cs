// <copyright file="TagSyntax.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Templating;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Allors.Documents.Expressions;

/// <summary>
/// The template tag grammar:
/// <list type="bullet">
/// <item><c>&lt;$path&gt;</c> — binding</item>
/// <item><c>&lt;@for item collection&gt;</c> (or <c>&lt;@for(item) collection&gt;</c>) — loop</item>
/// <item><c>&lt;@if expression&gt;</c> — conditional</item>
/// <item><c>&lt;@end&gt;</c> — closes the nearest open loop or conditional</item>
/// </list>
/// Tags live in <c>text:placeholder</c> elements; image substitution uses <c>draw:frame</c>
/// elements whose <c>draw:name</c> starts with <c>$</c>.
/// </summary>
internal static class TagSyntax
{
    internal const string TextNamespacePart = "opendocument:xmlns:text";

    internal const string DrawNamespacePart = "opendocument:xmlns:drawing";

    private static readonly Regex BindingRegex = new(@"^<\$\s*(.+?)\s*>$", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ForRegex = new(@"^<@for\b\s*(.*?)\s*>$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IfRegex = new(@"^<@if\b\s*(.*?)\s*>$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex EndRegex = new(@"^<@end\b.*>$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ForParenthesizedContentRegex = new(@"^\(\s*(\w+)\s*\)\s*(.+)$", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ForContentRegex = new(@"^(\w+)\s+(.+)$", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly HashSet<string> ReservedItemNames = new(StringComparer.Ordinal) { "i", "i0", "true", "false", "null" };

    internal static bool IsPlaceholder(XElement element) =>
        element.Name.LocalName.Equals("placeholder", StringComparison.Ordinal) &&
        element.Name.NamespaceName.Contains(TextNamespacePart, StringComparison.Ordinal);

    /// <summary>Classifies a placeholder element, or returns null when its text is not a template tag.</summary>
    internal static Tag? TryCreate(XElement placeholder)
    {
        var text = placeholder.Value.Trim();

        var match = BindingRegex.Match(text);
        if (match.Success)
        {
            return new BindingTag(placeholder, ExpressionParser.ParsePath(match.Groups[1].Value));
        }

        match = ForRegex.Match(text);
        if (match.Success)
        {
            return CreateFor(placeholder, text, match.Groups[1].Value);
        }

        match = IfRegex.Match(text);
        if (match.Success)
        {
            return new IfTag(placeholder, ExpressionParser.Parse(match.Groups[1].Value));
        }

        if (EndRegex.IsMatch(text))
        {
            return new EndTag(placeholder);
        }

        return null;
    }

    /// <summary>Classifies a drawing frame, or returns null when its name is not a template expression.</summary>
    internal static FrameTag? TryCreateFrame(XElement element)
    {
        if (!element.Name.LocalName.Equals("frame", StringComparison.Ordinal) ||
            !element.Name.NamespaceName.Contains(DrawNamespacePart, StringComparison.Ordinal))
        {
            return null;
        }

        var nameAttribute = element.Attributes().FirstOrDefault(attribute =>
            attribute.Name.LocalName.Equals("name", StringComparison.Ordinal) &&
            attribute.Name.NamespaceName.Contains(DrawNamespacePart, StringComparison.Ordinal));

        if (nameAttribute is null)
        {
            return null;
        }

        var name = nameAttribute.Value.Trim();
        if (!name.StartsWith('$'))
        {
            return null;
        }

        return new FrameTag(element, nameAttribute, ExpressionParser.ParsePath(name.Substring(1)));
    }

    private static ForTag CreateFor(XElement placeholder, string text, string content)
    {
        var match = ForParenthesizedContentRegex.Match(content);
        if (!match.Success)
        {
            match = ForContentRegex.Match(content);
        }

        if (!match.Success)
        {
            throw new TemplateException($"Invalid for tag '{text}': expected '<@for item collection>'.");
        }

        var itemName = match.Groups[1].Value;
        if (ReservedItemNames.Contains(itemName))
        {
            throw new TemplateException($"Invalid for tag '{text}': '{itemName}' is a reserved name.");
        }

        var collection = ExpressionParser.ParsePath(match.Groups[2].Value);
        return new ForTag(placeholder, itemName, collection);
    }
}
