// <copyright file="TagSyntax.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Tags
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The tag language of a template: <c>&lt;$path attr="value"&gt;</c>, <c>&lt;@for variable path&gt;</c>, <c>&lt;@if path&gt;</c> and <c>&lt;@end&gt;</c> in placeholder fields,
    /// and <c>$path</c> as the name of a picture frame. A path is a chain of member names; keywords are not case-sensitive.
    /// </summary>
    public static class TagSyntax
    {
        private static readonly Regex PathRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$");

        private static readonly Regex NameRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");

        private static readonly Regex AttributeRegex = new Regex(@"\G\s+([A-Za-z_][A-Za-z0-9_-]*)\s*=\s*(?:""([^""]*)""|'([^']*)')");

        /// <summary>True when the text has the shape of a tag: it starts with <c>&lt;$</c> or <c>&lt;@</c> and ends with <c>&gt;</c>.</summary>
        public static bool IsTag(string text)
        {
            var trimmed = text?.Trim();
            return trimmed != null && trimmed.Length >= 3 && trimmed[0] == '<' && (trimmed[1] == '$' || trimmed[1] == '@') && trimmed[trimmed.Length - 1] == '>';
        }

        /// <summary>True when the text is a plain path: member names separated by dots.</summary>
        public static bool IsPath(string text) => text != null && PathRegex.IsMatch(text);

        /// <summary>Parses the text of a placeholder. On failure the error says what is wrong, in the author's terms.</summary>
        public static bool TryParse(string text, out Tag tag, out string error)
        {
            tag = null;
            error = null;

            var trimmed = text?.Trim() ?? string.Empty;
            if (!IsTag(trimmed))
            {
                error = "not a tag: a tag starts with <$ or <@ and ends with >";
                return false;
            }

            var inner = trimmed.Substring(2, trimmed.Length - 3).Trim();

            if (trimmed[1] == '$')
            {
                return TryParseBinding(trimmed, inner, out tag, out error);
            }

            var space = IndexOfWhitespace(inner);
            var keyword = (space < 0 ? inner : inner.Substring(0, space)).ToLowerInvariant();
            var rest = space < 0 ? string.Empty : inner.Substring(space).Trim();

            switch (keyword)
            {
                case "for":
                    var variableEnd = IndexOfWhitespace(rest);
                    if (variableEnd < 0)
                    {
                        error = "<@for> needs a variable and a path: <@for item Lines>";
                        return false;
                    }

                    var variable = rest.Substring(0, variableEnd);
                    var collection = rest.Substring(variableEnd).Trim();
                    if (!NameRegex.IsMatch(variable))
                    {
                        error = "the loop variable '" + variable + "' is not a name";
                        return false;
                    }

                    if (!PathRegex.IsMatch(collection))
                    {
                        error = "'" + collection + "' is not a path";
                        return false;
                    }

                    tag = new Tag(TagKind.For, trimmed, collection, variable, null);
                    return true;

                case "if":
                    if (!PathRegex.IsMatch(rest))
                    {
                        error = rest.Length == 0 ? "<@if> needs a path: <@if Invoice.Comment>" : "'" + rest + "' is not a path";
                        return false;
                    }

                    tag = new Tag(TagKind.If, trimmed, rest, null, null);
                    return true;

                case "end":
                    tag = new Tag(TagKind.End, trimmed, null, null, null);
                    return true;

                default:
                    error = "unknown tag <@" + keyword + ">: expected for, if or end";
                    return false;
            }
        }

        /// <summary>The path a picture frame is bound to, when its name starts with <c>$</c>.</summary>
        public static bool TryParsePicture(string frameName, out string path, out string error)
        {
            path = null;
            error = null;

            var trimmed = frameName?.Trim() ?? string.Empty;
            if (trimmed.Length < 2 || trimmed[0] != '$')
            {
                error = "not a bound picture: the frame name starts with $";
                return false;
            }

            var candidate = trimmed.Substring(1).Trim();
            if (!PathRegex.IsMatch(candidate))
            {
                error = "'" + candidate + "' is not a path";
                return false;
            }

            path = candidate;
            return true;
        }

        private static bool TryParseBinding(string text, string inner, out Tag tag, out string error)
        {
            tag = null;
            error = null;

            var pathEnd = IndexOfWhitespace(inner);
            var path = pathEnd < 0 ? inner : inner.Substring(0, pathEnd);
            if (!PathRegex.IsMatch(path))
            {
                error = path.Length == 0 ? "<$> needs a path: <$Invoice.Number>" : "'" + path + "' is not a path";
                return false;
            }

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var position = path.Length;
            while (position < inner.Length)
            {
                var match = AttributeRegex.Match(inner, position);
                if (!match.Success)
                {
                    error = "unexpected text after the path: '" + inner.Substring(position).Trim() + "' (attributes look like format=\"...\")";
                    return false;
                }

                attributes[match.Groups[1].Value] = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                position = match.Index + match.Length;
            }

            tag = new Tag(TagKind.Binding, text, path, null, attributes);
            return true;
        }

        private static int IndexOfWhitespace(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
