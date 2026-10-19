// <copyright file="TagSyntax.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The tag language of a template: <c>&lt;$path attr="value"&gt;</c>, <c>&lt;@for variable path sort="key desc" index="n" separator=", "&gt;</c>,
    /// <c>&lt;@if path&gt;</c>, <c>&lt;@ifdef path&gt;</c>, both with <c>not</c> before the path to negate them, <c>&lt;@else&gt;</c>, <c>&lt;@else if path&gt;</c>,
    /// <c>&lt;@else ifdef path&gt;</c>, <c>&lt;@map Name&gt;</c> and <c>&lt;@end&gt;</c> in placeholder fields, and <c>&lt;$path empty="keep" fit="contain"&gt;</c>
    /// as the description of a picture frame. A path is a chain of member names; keywords are not case-sensitive.
    /// </summary>
    internal static class TagSyntax
    {
        private static readonly Regex PathRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$");

        private static readonly Regex NameRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");

        /// <summary>The attribute that orders a loop.</summary>
        public const string SortAttribute = "sort";

        /// <summary>The attribute that names a loop's counter.</summary>
        public const string IndexAttribute = "index";

        /// <summary>The attribute that gives the text a loop writes between two items.</summary>
        public const string SeparatorAttribute = "separator";

        /// <summary>The attribute that names the map a binding prints its value through.</summary>
        public const string MapAttribute = "map";

        /// <summary>The attribute that gives the text a binding prints when its value is blank: null, or text that is empty or white space.</summary>
        public const string BlankAttribute = "blank";

        /// <summary>The key of a map's row that serves every value the map has no row for.</summary>
        public const string DefaultKey = "default";

        /// <summary>The keyword that negates a condition.</summary>
        public const string NotKeyword = "not";

        /// <summary>The attribute that says what a picture frame does without a picture: <see cref="RemoveValue"/>, the default, or <see cref="KeepValue"/>.</summary>
        public const string EmptyAttribute = "empty";

        /// <summary>The frame leaves the document when there is no picture.</summary>
        public const string RemoveValue = "remove";

        /// <summary>The frame keeps the template's picture when there is no picture.</summary>
        public const string KeepValue = "keep";

        /// <summary>The attribute that says how a picture takes its frame: <see cref="FillValue"/>, the default, <see cref="ContainValue"/>, <see cref="WidthValue"/> or <see cref="HeightValue"/>.</summary>
        public const string FitAttribute = "fit";

        /// <summary>The picture is stretched to the frame as the author drew it.</summary>
        public const string FillValue = "fill";

        /// <summary>The picture keeps its proportions inside the frame; the side that would overflow shrinks.</summary>
        public const string ContainValue = "contain";

        /// <summary>The frame keeps its width; its height follows the picture's proportions.</summary>
        public const string WidthValue = "width";

        /// <summary>The frame keeps its height; its width follows the picture's proportions.</summary>
        public const string HeightValue = "height";

        /// <summary>The attributes a picture tag takes: empty, fit and format, which names a picture renderer.</summary>
        public static readonly IReadOnlyCollection<string> PictureAttributes = new[] { EmptyAttribute, FitAttribute, Formatting.FormatAttribute };

        /// <summary>The attributes a binding takes: the formatting attributes, then map and blank.</summary>
        public static readonly IReadOnlyCollection<string> BindingAttributes = new[] { Formatting.FormatAttribute, Formatting.CultureSetting, Formatting.TrueSetting, Formatting.FalseSetting, MapAttribute, BlankAttribute };

        /// <summary>The attributes a loop takes.</summary>
        public static readonly IReadOnlyCollection<string> LoopAttributes = new[] { SortAttribute, IndexAttribute, SeparatorAttribute };

        private static readonly Regex AttributeRegex = new Regex(@"\G\s+([A-Za-z_][A-Za-z0-9_-]*)\s*=\s*(?:""([^""]*)""|'([^']*)')");

        /// <summary>True when the text has the shape of a tag: it starts with <c>&lt;$</c> or <c>&lt;@</c> and ends with <c>&gt;</c>.</summary>
        public static bool IsTag(string text)
        {
            var trimmed = text?.Trim();
            return trimmed != null && trimmed.Length >= 3 && trimmed[0] == '<' && (trimmed[1] == '$' || trimmed[1] == '@') && trimmed[trimmed.Length - 1] == '>';
        }

        /// <summary>True when the text holds the start of a tag, <c>&lt;$</c> or <c>&lt;@</c>, anywhere: the author meant a tag, well-formed or not.</summary>
        public static bool HoldsTag(string text) => text != null && (text.Contains("<$") || text.Contains("<@"));

        /// <summary>True for an attribute a picture tag takes; names are not case-sensitive.</summary>
        public static bool IsPictureAttribute(string name) => Contains(PictureAttributes, name);

        /// <summary>True for an attribute a binding takes; names are not case-sensitive.</summary>
        public static bool IsBindingAttribute(string name) => Contains(BindingAttributes, name);

        /// <summary>True for an attribute a loop takes; names are not case-sensitive.</summary>
        public static bool IsLoopAttribute(string name) => Contains(LoopAttributes, name);

        /// <summary>True when a map's key is the default key, in any case.</summary>
        public static bool IsDefaultKey(string key) => string.Equals(key?.Trim(), DefaultKey, StringComparison.OrdinalIgnoreCase);

        /// <summary>True for a value the empty attribute takes: remove or keep, in any case.</summary>
        public static bool IsEmptyValue(string value) => string.Equals(value?.Trim(), RemoveValue, StringComparison.OrdinalIgnoreCase) || KeepsWhenEmpty(value);

        /// <summary>True when the value of the empty attribute says keep.</summary>
        public static bool KeepsWhenEmpty(string value) => string.Equals(value?.Trim(), KeepValue, StringComparison.OrdinalIgnoreCase);

        /// <summary>Reads a value of the fit attribute, in any case: fill, contain, width or height; false for a value the attribute does not take.</summary>
        public static bool TryParseFit(string value, out PictureFit fit)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case FillValue:
                    fit = PictureFit.Fill;
                    return true;
                case ContainValue:
                    fit = PictureFit.Contain;
                    return true;
                case WidthValue:
                    fit = PictureFit.Width;
                    return true;
                case HeightValue:
                    fit = PictureFit.Height;
                    return true;
                default:
                    fit = PictureFit.Fill;
                    return false;
            }
        }

        /// <summary>The fit a picture tag asks for: the value of its fit attribute; fill without one, or with a value the attribute does not take.</summary>
        public static PictureFit FitOf(Tag tag) => tag.Attributes.TryGetValue(FitAttribute, out var value) && TryParseFit(value, out var fit) ? fit : PictureFit.Fill;

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
                    if (!NameRegex.IsMatch(variable))
                    {
                        error = "the loop variable '" + variable + "' is not a name";
                        return false;
                    }

                    var afterVariable = rest.Substring(variableEnd).Trim();
                    if (!TryParsePathAndAttributes(afterVariable, "<@for> needs a path after the variable: <@for item Lines>", out var collection, out var loopAttributes, out error))
                    {
                        return false;
                    }

                    if (!TryParseSort(loopAttributes, out var sort, out error))
                    {
                        return false;
                    }

                    if (!TryParseIndex(loopAttributes, variable, out var index, out error))
                    {
                        return false;
                    }

                    var separator = loopAttributes.TryGetValue(SeparatorAttribute, out var separatorText) && separatorText.Length > 0 ? separatorText : null;
                    tag = new Tag(TagKind.For, trimmed, collection, variable, loopAttributes, sort, index, separator);
                    return true;

                case "if":
                case "ifdef":
                    if (!TryParseCondition(keyword, rest, out var path, out var negated, out error))
                    {
                        return false;
                    }

                    tag = new Tag(keyword == "if" ? TagKind.If : TagKind.IfDef, trimmed, path, null, null, defined: keyword == "ifdef", negated: negated);
                    return true;

                case "else":
                    if (rest.Length == 0)
                    {
                        tag = new Tag(TagKind.Else, trimmed, null, null, null);
                        return true;
                    }

                    var testEnd = IndexOfWhitespace(rest);
                    var test = (testEnd < 0 ? rest : rest.Substring(0, testEnd)).ToLowerInvariant();
                    if (test != "if" && test != "ifdef")
                    {
                        error = "unexpected text after else: '" + rest + "' (an else is <@else>, <@else if path> or <@else ifdef path>)";
                        return false;
                    }

                    if (!TryParseCondition("else " + test, testEnd < 0 ? string.Empty : rest.Substring(testEnd).Trim(), out var testPath, out var testNegated, out error))
                    {
                        return false;
                    }

                    tag = new Tag(TagKind.Else, trimmed, testPath, null, null, defined: test == "ifdef", negated: testNegated);
                    return true;

                case "map":
                    if (!NameRegex.IsMatch(rest))
                    {
                        error = rest.Length == 0 ? "<@map> needs a name: <@map Titles>" : "the map's name '" + rest + "' is not a name";
                        return false;
                    }

                    tag = new Tag(TagKind.Map, trimmed, rest, null, null);
                    return true;

                case "end":
                    tag = new Tag(TagKind.End, trimmed, null, null, null);
                    return true;

                default:
                    error = "unknown tag <@" + keyword + ">: expected for, if, ifdef, else, map or end";
                    return false;
            }
        }

        /// <summary>
        /// Parses the description of a picture frame as a picture tag: one <c>&lt;$path attr="value"&gt;</c> and nothing else.
        /// On failure the error says what is wrong, in the author's terms.
        /// </summary>
        public static bool TryParsePicture(string description, out Tag tag, out string error)
        {
            tag = null;
            error = null;

            var trimmed = description?.Trim() ?? string.Empty;
            if (!IsTag(trimmed))
            {
                error = "a picture tag is the whole description: <$Issuer.Logo>, with nothing before or after it";
                return false;
            }

            if (trimmed[1] == '@')
            {
                error = "a picture takes a <$path> tag, not a block tag";
                return false;
            }

            if (trimmed.IndexOf('<', 1) >= 0 || trimmed.IndexOf('>') != trimmed.Length - 1)
            {
                error = "a picture takes one <$path> tag: the description holds more";
                return false;
            }

            var inner = trimmed.Substring(2, trimmed.Length - 3).Trim();
            if (!TryParsePathAndAttributes(inner, "<$> needs a path: <$Issuer.Logo>", out var path, out var attributes, out error))
            {
                return false;
            }

            tag = new Tag(TagKind.Picture, trimmed, path, null, attributes);
            return true;
        }

        private static bool TryParseBinding(string text, string inner, out Tag tag, out string error)
        {
            tag = null;

            if (!TryParsePathAndAttributes(inner, "<$> needs a path: <$Invoice.Number>", out var path, out var attributes, out error))
            {
                return false;
            }

            tag = new Tag(TagKind.Binding, text, path, null, attributes);
            return true;
        }

        /// <summary>The test of a condition: a path, with not before it to negate the test.</summary>
        private static bool TryParseCondition(string keyword, string rest, out string path, out bool negated, out string error)
        {
            path = null;
            negated = false;
            error = null;

            var text = rest;
            var notEnd = IndexOfWhitespace(text);
            if (notEnd > 0 && string.Equals(text.Substring(0, notEnd), NotKeyword, StringComparison.OrdinalIgnoreCase))
            {
                negated = true;
                text = text.Substring(notEnd).Trim();
            }

            if (!PathRegex.IsMatch(text))
            {
                error = text.Length == 0 ? "<@" + keyword + "> needs a path: <@" + keyword + " Invoice.Comment>" : "'" + text + "' is not a path";
                return false;
            }

            path = text;
            return true;
        }

        /// <summary>A path followed by zero or more name="value" attributes.</summary>
        private static bool TryParsePathAndAttributes(string inner, string missingPathError, out string path, out Dictionary<string, string> attributes, out string error)
        {
            attributes = null;
            error = null;

            var pathEnd = IndexOfWhitespace(inner);
            path = pathEnd < 0 ? inner : inner.Substring(0, pathEnd);
            if (!PathRegex.IsMatch(path))
            {
                error = path.Length == 0 ? missingPathError : "'" + path + "' is not a path";
                return false;
            }

            attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

            return true;
        }

        /// <summary>The sort attribute of a loop: paths relative to the item, comma separated, each optionally followed by asc or desc.</summary>
        private static bool TryParseSort(IReadOnlyDictionary<string, string> attributes, out IReadOnlyList<SortKey> sort, out string error)
        {
            sort = null;
            error = null;

            if (!attributes.TryGetValue(SortAttribute, out var text))
            {
                return true;
            }

            var keys = new List<SortKey>();
            foreach (var part in text.Split(','))
            {
                var key = part.Trim();
                if (key.Length == 0)
                {
                    error = "sort=\"" + text + "\": a key is missing between the commas";
                    return false;
                }

                var space = IndexOfWhitespace(key);
                var path = space < 0 ? key : key.Substring(0, space);
                var direction = space < 0 ? string.Empty : key.Substring(space).Trim().ToLowerInvariant();

                if (!PathRegex.IsMatch(path))
                {
                    error = "sort=\"" + text + "\": '" + path + "' is not a path";
                    return false;
                }

                if (direction.Length > 0 && direction != "asc" && direction != "desc")
                {
                    error = "sort=\"" + text + "\": expected asc or desc after '" + path + "', not '" + direction + "'";
                    return false;
                }

                keys.Add(new SortKey(path, direction == "desc"));
            }

            sort = keys;
            return true;
        }

        /// <summary>The index attribute of a loop: the name under which the body reads the item's position, 1 for the first item in the loop's order.</summary>
        private static bool TryParseIndex(IReadOnlyDictionary<string, string> attributes, string variable, out string index, out string error)
        {
            index = null;
            error = null;

            if (!attributes.TryGetValue(IndexAttribute, out var text))
            {
                return true;
            }

            var name = text.Trim();
            if (!NameRegex.IsMatch(name))
            {
                error = "index=\"" + text + "\": the counter's name '" + name + "' is not a name";
                return false;
            }

            if (name == variable)
            {
                error = "index=\"" + text + "\": the counter needs a name other than the loop variable's";
                return false;
            }

            index = name;
            return true;
        }

        private static bool Contains(IEnumerable<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
