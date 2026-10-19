// <copyright file="Validator.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Xml;

    /// <summary>Checks an inspected template against a model shape.</summary>
    internal sealed class Validator
    {
        private static readonly Location SettingsLocation = new Location("meta.xml", 0, string.Empty);

        /// <summary>A loop counter is a number: it has no members and is not a collection.</summary>
        private static readonly IShape CounterShape = new ValueShape(ValueKind.Number, "the loop counter");

        private readonly IShape root;
        private readonly Renderers renderers;
        private readonly List<Finding> findings = new List<Finding>();
        private readonly Dictionary<InspectedTag, Scope> scopeByBranch = new Dictionary<InspectedTag, Scope>();
        private readonly HashSet<XmlElement> blockElements = new HashSet<XmlElement>();
        private readonly HashSet<XmlElement> containersChecked = new HashSet<XmlElement>();
        private readonly Dictionary<string, InspectedMap> mapsByName = new Dictionary<string, InspectedMap>();
        private readonly HashSet<InspectedMap> mapsUsed = new HashSet<InspectedMap>();

        private Validator(IShape root, Renderers? renderers)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.renderers = renderers ?? Renderers.Default;
        }

        /// <summary>Validates against the shape and the template's renderers; null stands for the built-in renderers alone.</summary>
        public static TemplateValidation Validate(TemplateInspection inspection, IShape root, Renderers? renderers = null)
        {
            var validator = new Validator(root, renderers);
            validator.Run(inspection);
            return new TemplateValidation(validator.Sorted(), inspection);
        }

        private void Run(TemplateInspection inspection)
        {
            foreach (var problem in inspection.Problems)
            {
                this.Add(problem.IsUnbalanced ? FindingKind.UnbalancedBlock : FindingKind.MalformedTag, FindingSeverity.Error, problem.Text, null, problem.Message, problem.Location);
            }

            foreach (var tag in inspection.All)
            {
                if (tag.Tag.Kind != TagKind.Binding && tag.Element != null)
                {
                    this.blockElements.Add(tag.Element);
                }
            }

            foreach (var map in inspection.Maps)
            {
                this.mapsByName[map.Name] = map;
            }

            var rootScope = new Scope(null, null, null);
            foreach (var tag in inspection.Tags)
            {
                this.Visit(tag, rootScope);
            }

            foreach (var picture in inspection.Pictures)
            {
                var scope = picture.Branch == null ? rootScope : this.scopeByBranch[picture.Branch];
                this.CheckPicture(picture, scope);
            }

            this.CheckMaps(inspection.Maps);
            this.CheckSettings(inspection.Settings);
        }

        /// <summary>
        /// A frame's tag binds a picture, or a value a picture renderer draws when its format names one; the attributes are empty, fit and format.
        /// </summary>
        private void CheckPicture(InspectedPicture picture, Scope scope)
        {
            var tag = picture.Tag;
            var shape = this.CheckPath(picture.Path, scope, tag.Text, picture.Location);

            var drawn = false;
            foreach (var attribute in tag.Attributes)
            {
                if (!TagSyntax.IsPictureAttribute(attribute.Key))
                {
                    this.Add(FindingKind.UnknownAttribute, FindingSeverity.Error, tag.Text, null, "a picture has no attribute '" + attribute.Key + "': use " + string.Join(", ", TagSyntax.PictureAttributes), picture.Location);
                }
                else if (string.Equals(attribute.Key, TagSyntax.FitAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    if (!TagSyntax.TryParseFit(attribute.Value, out _))
                    {
                        this.Add(FindingKind.BadValue, FindingSeverity.Error, tag.Text, null, TagSyntax.FitAttribute + "=\"" + attribute.Value + "\": use " + TagSyntax.FillValue + ", " + TagSyntax.ContainValue + ", " + TagSyntax.WidthValue + " or " + TagSyntax.HeightValue, picture.Location);
                    }
                }
                else if (string.Equals(attribute.Key, Formatting.FormatAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    drawn = this.CheckPictureFormat(attribute.Value, tag.Text, picture.Location);
                }
                else if (!TagSyntax.IsEmptyValue(attribute.Value))
                {
                    this.Add(FindingKind.BadValue, FindingSeverity.Error, tag.Text, null, TagSyntax.EmptyAttribute + "=\"" + attribute.Value + "\": use " + TagSyntax.RemoveValue + " or " + TagSyntax.KeepValue, picture.Location);
                }
            }

            // A picture renderer draws the frame's picture for a value of any kind; without one the value must be a picture
            if (!drawn && shape != null && shape.Kind != ValueKind.Picture && shape.Kind != ValueKind.Open)
            {
                this.Add(FindingKind.NotAPicture, FindingSeverity.Error, tag.Text, tag.Path, "'" + tag.Path + "' is " + KindText(shape) + ", not a picture: bind the frame to a member that holds the bytes of a picture", picture.Location);
            }
        }

        /// <summary>A picture tag's format names a picture renderer and nothing else; true when it does.</summary>
        private bool CheckPictureFormat(string format, string text, Location location)
        {
            var use = this.renderers.PictureNames.Length == 0 ? "none is registered" : "use " + this.renderers.PictureNames;
            if (!this.renderers.TryGet(format, out var renderer))
            {
                this.Add(FindingKind.BadValue, FindingSeverity.Error, text, null, Formatting.FormatAttribute + "=\"" + format + "\" names no picture renderer: " + use, location);
                return false;
            }

            if (!(renderer is IPictureRenderer))
            {
                this.Add(FindingKind.BadValue, FindingSeverity.Error, text, null, Formatting.FormatAttribute + "=\"" + format + "\" renders text, not a picture: " + use, location);
                return false;
            }

            return true;
        }

        private void Visit(InspectedTag tag, Scope scope)
        {
            switch (tag.Tag.Kind)
            {
                case TagKind.Binding:
                    this.CheckBinding(tag, scope);
                    break;

                case TagKind.If:
                case TagKind.IfDef:
                    this.VisitCondition(tag, scope);
                    break;

                case TagKind.For:
                    var body = this.CheckLoop(tag, scope);
                    this.scopeByBranch[tag] = body;
                    var pair = this.CheckStructure(tag);
                    if (tag.Tag.Separator != null && pair?.BeginSibling is XmlElement begin && !BlockStructure.IsInline(pair))
                    {
                        this.Add(FindingKind.SeparatorIgnored, FindingSeverity.Warning, tag.Tag.Text, null, "the separator is not written: it goes between the items of a loop that sits in one paragraph, and this loop's tags sit in " + BlockStructure.Describe(begin) + "s of their own", tag.Location);
                    }

                    foreach (var child in tag.Children)
                    {
                        this.Visit(child, body);
                    }

                    break;
            }
        }

        /// <summary>
        /// Each branch of a condition is checked in its own scope: the test of the first branch narrows the guarded path to the variants that
        /// have the member, and what follows an else is checked as if that test had failed, so an ifdef's else sees the variants without the
        /// member; an else with a test of its own narrows again.
        /// </summary>
        private void VisitCondition(InspectedTag tag, Scope scope)
        {
            this.CheckStructure(tag);

            var branch = this.Branch(tag, scope, out var next);
            this.scopeByBranch[tag] = branch;
            foreach (var child in tag.Children)
            {
                if (child.Tag.Kind == TagKind.Else)
                {
                    branch = child.Tag.HasTest ? this.Branch(child, next, out next) : next;
                    this.scopeByBranch[child] = branch;
                    continue;
                }

                this.Visit(child, branch);
            }
        }

        /// <summary>
        /// The scope of the branch a test opens, and the scope in which the next branch's test is evaluated. Where the test's path reaches a
        /// member only some variants have, the branch where the raw test is true sees those variants; for an ifdef on the last segment the
        /// branch where it is false sees the others. Not swaps the two.
        /// </summary>
        private Scope Branch(InspectedTag test, Scope evaluation, out Scope fallthrough)
        {
            var holds = new Scope(evaluation, null, null);
            fallthrough = new Scope(evaluation, null, null);

            // A condition, and an else with a test, has a path
            var path = test.Tag.Path!;
            var split = new Split();
            if (!this.TryResolve(path, evaluation, split, out _, out var failure))
            {
                this.AddUnknownMember(failure, path, test.Tag.Text, test.Location);
                return holds;
            }

            var whenTrue = test.Tag.Negated ? fallthrough : holds;
            var whenFalse = test.Tag.Negated ? holds : fallthrough;
            foreach (var narrowing in split.Narrowings)
            {
                whenTrue.Narrow(narrowing.Base, narrowing.Prefix, Shape(narrowing.With));
            }

            var last = split.Narrowings.Count == 1 ? split.Narrowings[0] : null;
            if (test.Tag.Defined && last != null && last.IsLast && last.Without.Count > 0)
            {
                whenFalse.Narrow(last.Base, last.Prefix, Shape(last.Without));
            }

            return holds;
        }

        private static IShape Shape(IReadOnlyList<IShape> variants) => variants.Count == 1 ? variants[0] : new NarrowedShape(variants);

        private void CheckBinding(InspectedTag tag, Scope scope)
        {
            // A binding has a path
            var shape = this.CheckPath(tag.Tag.Path!, scope, tag.Tag.Text, tag.Location);

            foreach (var attribute in tag.Tag.Attributes)
            {
                if (!TagSyntax.IsBindingAttribute(attribute.Key))
                {
                    this.Add(FindingKind.UnknownAttribute, FindingSeverity.Error, tag.Tag.Text, null, "a binding has no attribute '" + attribute.Key + "': use " + string.Join(", ", TagSyntax.BindingAttributes), tag.Location);
                }
                else if (string.Equals(attribute.Key, Formatting.CultureSetting, StringComparison.OrdinalIgnoreCase))
                {
                    this.CheckCulture(attribute.Value, tag.Tag.Text, tag.Location);
                }
                else if (string.Equals(attribute.Key, TagSyntax.MapAttribute, StringComparison.OrdinalIgnoreCase))
                {
                    if (this.mapsByName.TryGetValue(attribute.Value, out var map))
                    {
                        this.mapsUsed.Add(map);
                    }
                    else
                    {
                        this.Add(FindingKind.UnknownMap, FindingSeverity.Error, tag.Tag.Text, null, "no map '" + attribute.Value + "' in the template: a map is a table whose first cell holds <@map " + attribute.Value + ">", tag.Location);
                    }
                }
            }

            var kind = PrintedKind(shape);
            if (tag.Tag.Attributes.TryGetValue(Formatting.FormatAttribute, out var format) && !string.IsNullOrEmpty(format))
            {
                this.CheckFormat(tag, format, kind);
            }

            if ((tag.Tag.Attributes.ContainsKey(Formatting.TrueSetting) || tag.Tag.Attributes.ContainsKey(Formatting.FalseSetting)) && shape != null && kind != ValueKind.Boolean && kind != ValueKind.Open)
            {
                this.Add(FindingKind.FormatIgnored, FindingSeverity.Warning, tag.Tag.Text, tag.Tag.Path, "the true and false words are ignored: '" + tag.Tag.Path + "' is " + KindText(shape) + ", not a boolean", tag.Location);
            }
        }

        /// <summary>
        /// A format must be of the family of the kind it prints: a date, time or date-time takes a date pattern, a number a number pattern,
        /// and a kind that prints without a pattern ignores it. An open or other value takes any pattern of either family.
        /// </summary>
        private void CheckFormat(InspectedTag tag, string format, ValueKind kind)
        {
            var path = tag.Tag.Path;
            if (this.renderers.TryGet(format, out var renderer))
            {
                if (!(renderer is ITextRenderer))
                {
                    this.Add(FindingKind.BadValue, FindingSeverity.Error, tag.Tag.Text, path, Formatting.FormatAttribute + "=\"" + format + "\" renders a picture, not text: use " + this.renderers.TextNames + ", a pattern, or date, time, datetime or number for the document's pattern", tag.Location);
                }

                return;
            }

            var named = Formatting.KindOfSetting(format);
            switch (kind)
            {
                case ValueKind.Date:
                case ValueKind.Time:
                case ValueKind.DateTime:
                    if (named == ValueKind.Number)
                    {
                        this.Add(FindingKind.BadPattern, FindingSeverity.Error, tag.Tag.Text, path, "'" + format + "' names the document's number pattern, and '" + path + "' is " + KindText(kind) + ": use date, time or datetime, or a date pattern", tag.Location);
                    }
                    else if (named == null && !Patterns.IsDate(format))
                    {
                        this.Add(FindingKind.BadPattern, FindingSeverity.Error, tag.Tag.Text, path, "'" + format + "' is not a date pattern, and '" + path + "' is " + KindText(kind) + ": use the letters d, M, y, h, H, m, s, f, F, t, z, K and g with separators and quoted text, such as dd-MMM-yyyy, or a standard letter such as d, D or g", tag.Location);
                    }

                    break;

                case ValueKind.Number:
                    if (named != null && named != ValueKind.Number)
                    {
                        this.Add(FindingKind.BadPattern, FindingSeverity.Error, tag.Tag.Text, path, "'" + format + "' names the document's " + format.ToLowerInvariant() + " pattern, and '" + path + "' is a number: use number, or a number pattern", tag.Location);
                    }
                    else if (named == null && !Patterns.IsNumber(format))
                    {
                        this.Add(FindingKind.BadPattern, FindingSeverity.Error, tag.Tag.Text, path, "'" + format + "' is not a number pattern, and '" + path + "' is a number: use a standard letter with its precision such as N2, F0 or C, or a pattern with 0 and # such as #,##0.00", tag.Location);
                    }

                    break;

                case ValueKind.Text:
                case ValueKind.Boolean:
                case ValueKind.Object:
                case ValueKind.Picture:
                    this.Add(FindingKind.FormatIgnored, FindingSeverity.Warning, tag.Tag.Text, path, "the format is ignored: '" + path + "' is " + KindText(kind) + " and prints without a pattern", tag.Location);
                    break;

                default:
                    if (named == null && !Patterns.IsDate(format) && !Patterns.IsNumber(format))
                    {
                        this.Add(FindingKind.BadPattern, FindingSeverity.Error, tag.Tag.Text, path, "'" + format + "' is not a format: use a date or number pattern, date, time, datetime or number for the document's pattern, or a renderer: " + this.renderers.TextNames, tag.Location);
                    }

                    break;
            }
        }

        /// <summary>The kind a binding prints: a collection prints its items, so their kind; an unknown path is open.</summary>
        private static ValueKind PrintedKind(IShape? shape)
        {
            while (shape != null && shape.Kind == ValueKind.Collection)
            {
                shape = shape.Element;
            }

            return shape?.Kind ?? ValueKind.Open;
        }

        private Scope CheckLoop(InspectedTag tag, Scope scope)
        {
            // A loop has a path and a variable
            var variable = tag.Tag.Variable;
            var path = tag.Tag.Path!;
            IShape variableShape;
            if (!this.TryResolve(path, scope, null, out var shape, out var failure))
            {
                this.AddUnknownMember(failure, path, tag.Tag.Text, tag.Location);
                variableShape = DictionaryShape.Open;
            }
            else if (shape.Kind == ValueKind.Collection || shape.Kind == ValueKind.Open)
            {
                variableShape = shape.Element ?? DictionaryShape.Open;
            }
            else
            {
                this.Add(FindingKind.NotACollection, FindingSeverity.Warning, tag.Tag.Text, path, "'" + path + "' is " + Describe(shape) + ", not a collection: the loop runs once over the value", tag.Location);
                variableShape = shape;
            }

            foreach (var attribute in tag.Tag.Attributes)
            {
                if (!TagSyntax.IsLoopAttribute(attribute.Key))
                {
                    this.Add(FindingKind.UnknownAttribute, FindingSeverity.Error, tag.Tag.Text, null, "a loop has no attribute '" + attribute.Key + "': use " + string.Join(", ", TagSyntax.LoopAttributes), tag.Location);
                }
            }

            foreach (var key in tag.Tag.Sort)
            {
                if (!TryResolveRelative(variableShape, key.Path, out var keyShape, out var keyFailure))
                {
                    this.Add(FindingKind.UnknownMember, FindingSeverity.Error, tag.Tag.Text, key.Path, "sort key '" + key.Path + "': " + UnknownMemberText(keyFailure), tag.Location);
                }
                else if (keyShape.Kind == ValueKind.Object || keyShape.Kind == ValueKind.Collection || keyShape.Kind == ValueKind.Picture)
                {
                    this.Add(FindingKind.CannotSort, FindingSeverity.Error, tag.Tag.Text, key.Path, "sort key '" + key.Path + "' is " + KindText(keyShape) + ", which does not order: sort by a text, number, boolean, date or time member of it, such as " + key.Path + ".Name", tag.Location);
                }
            }

            var body = new Scope(scope, variable, variableShape);
            if (tag.Tag.Index == null)
            {
                return body;
            }

            return new Scope(body, tag.Tag.Index, CounterShape);
        }

        /// <summary>The shape at the path, or null after reporting that the path breaks.</summary>
        private IShape? CheckPath(string path, Scope scope, string text, Location location)
        {
            if (!this.TryResolve(path, scope, null, out var shape, out var failure))
            {
                this.AddUnknownMember(failure, path, text, location);
            }

            return shape;
        }

        private void AddUnknownMember(Failure failure, string path, string text, Location location)
        {
            var message = UnknownMemberText(failure);
            if (failure.VariantsWith.Count > 0)
            {
                var guard = failure.Guard;
                message += ": put the block in <@ifdef " + guard + "> or <@if " + guard + ">";
            }

            this.Add(FindingKind.UnknownMember, FindingSeverity.Error, text, path, message, location);
        }

        private static string UnknownMemberText(Failure failure)
        {
            if (failure.VariantsWith.Count > 0)
            {
                return failure.Segment + " is a member of " + string.Join(" and ", failure.VariantsWith.Select(v => v.Name)) + " but not of every " + failure.At.Name;
            }

            return Describe(failure.At) + " has no member " + failure.Segment;
        }

        private static string Describe(IShape shape) => shape is DictionaryShape dictionary && dictionary.IsOpen ? "an open value" : shape.Name;

        /// <summary>The kind of a shape in the author's words, with the shape's name when it adds something: "an object (Party)", "text".</summary>
        private static string KindText(IShape shape)
        {
            var kind = KindText(shape.Kind);
            return shape.Kind == ValueKind.Object || shape.Kind == ValueKind.Collection || shape.Kind == ValueKind.Other ? kind + " (" + shape.Name + ")" : kind;
        }

        private static string KindText(ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.Open:
                    return "an open value";
                case ValueKind.Object:
                    return "an object";
                case ValueKind.Collection:
                    return "a collection";
                case ValueKind.Text:
                    return "text";
                case ValueKind.Number:
                    return "a number";
                case ValueKind.Boolean:
                    return "a boolean";
                case ValueKind.Date:
                    return "a date";
                case ValueKind.Time:
                    return "a time";
                case ValueKind.DateTime:
                    return "a date and time";
                case ValueKind.Picture:
                    return "a picture";
                default:
                    return "a value of another kind";
            }
        }

        /// <summary>
        /// Walks a path from a scope: the first segment is a loop variable in scope, innermost first, or a member of the root; the rest are members.
        /// With a split, a segment that only some variants have is noted in it and the walk goes on over those variants instead of failing.
        /// True with the shape the path reaches; false with where it failed.
        /// </summary>
        private bool TryResolve(string path, Scope scope, Split? split, [NotNullWhen(true)] out IShape? shape, [NotNullWhen(false)] out Failure? failure)
        {
            var segments = path.Split('.');
            var frame = scope.Frame(segments[0]);
            var @base = (object?)frame ?? this.root;
            var current = frame?.VariableShape ?? this.root;
            var start = frame == null ? 0 : 1;
            var prefix = string.Empty;

            current = scope.Narrowed(@base, prefix) ?? current;

            for (var i = start; i < segments.Length; i++)
            {
                var segment = segments[i];
                var member = current.Member(segment);
                if (member == null)
                {
                    var with = current.Variants.Where(v => v.Member(segment) != null).ToList();
                    if (with.Count == 0 || split == null)
                    {
                        shape = null;
                        failure = new Failure(current, segment, with, string.Join(".", segments.Take(i + 1)));
                        return false;
                    }

                    var without = current.Variants.Where(v => v.Member(segment) == null).ToList();
                    split.Narrowings.Add(new Narrowing(@base, prefix, with, without, i == segments.Length - 1));

                    // Every variant in with has the member, so their common shape has it too
                    member = Shape(with).Member(segment)!;
                }

                prefix = prefix.Length == 0 ? segment : prefix + "." + segment;
                current = scope.Narrowed(@base, prefix) ?? member;
            }

            shape = current;
            failure = null;
            return true;
        }

        /// <summary>Walks a path over a shape alone, as a sort key is relative to the item. True with the shape the path reaches; false with where it failed.</summary>
        private static bool TryResolveRelative(IShape shape, string path, [NotNullWhen(true)] out IShape? result, [NotNullWhen(false)] out Failure? failure)
        {
            var current = shape;
            var segments = path.Split('.');
            for (var i = 0; i < segments.Length; i++)
            {
                var member = current.Member(segments[i]);
                if (member == null)
                {
                    result = null;
                    failure = new Failure(current, segments[i], current.Variants.Where(v => v.Member(segments[i]) != null).ToList(), string.Join(".", segments.Take(i + 1)));
                    return false;
                }

                current = member;
            }

            result = current;
            failure = null;
            return true;
        }

        /// <summary>
        /// What the renderer does with a block: it takes the lowest common ancestor of its tags and removes the children of that ancestor that
        /// hold them, keeping or repeating what sits between. Tags in one paragraph cost only their text runs; tags in two cells of one row cost
        /// the cells; a tag inside a table whose partner is outside costs the whole table. The block's pair, for what else depends on its shape; null when it has none.
        /// </summary>
        private XmlPair? CheckStructure(InspectedTag block)
        {
            var end = block.End;
            if (end == null)
            {
                return null;
            }

            var pair = new XmlPair(null, block.Element) { End = end.Element };
            var elses = block.Elses.ToList();
            foreach (var @else in elses)
            {
                pair.AddSeparator(@else.Element);
            }

            var begin = pair.BeginSibling;
            var finish = pair.EndSibling;
            if (begin == null || finish == null)
            {
                return null;
            }

            var problem = BlockStructure.Problem(pair);
            if (problem != null)
            {
                this.Add(FindingKind.BlockStructure, FindingSeverity.Error, block.Tag.Text, null, BlockStructure.Message(problem), block.Location);
                return pair;
            }

            this.CheckMixedText(begin, block);
            for (var i = 0; i < elses.Count; i++)
            {
                // Without a problem every else sits in a child of the container
                if (pair.SeparatorSiblings[i] is XmlElement separator)
                {
                    this.CheckMixedText(separator, elses[i]);
                }
            }

            this.CheckMixedText(finish, end);
            return pair;
        }

        /// <summary>The element that holds a block tag is removed with everything else in it; say so when there is something else.</summary>
        private void CheckMixedText(XmlElement container, InspectedTag tag)
        {
            if (this.blockElements.Contains(container) || !this.containersChecked.Add(container))
            {
                return;
            }

            var text = new StringBuilder();
            CollectText(container, text, this.blockElements);
            var rest = text.ToString().Trim();
            if (rest.Length == 0)
            {
                return;
            }

            if (rest.Length > 60)
            {
                rest = rest.Substring(0, 60) + "...";
            }

            var what = BlockStructure.Describe(container);
            this.Add(FindingKind.MixedText, FindingSeverity.Warning, tag.Tag.Text, null, "the " + what + " that holds this tag is removed with the tag, and '" + rest + "' with it: give the tag its own " + what, tag.Location);
        }

        /// <summary>A map no binding prints through, and a map without a default row, are worth a look.</summary>
        private void CheckMaps(IEnumerable<InspectedMap> maps)
        {
            foreach (var map in maps)
            {
                if (!this.mapsUsed.Contains(map))
                {
                    this.Add(FindingKind.UnusedMap, FindingSeverity.Warning, map.Tag.Text, null, "no binding prints through the map '" + map.Name + "': use <$path map=\"" + map.Name + "\">", map.Location);
                }

                if (!map.HasDefault)
                {
                    this.Add(FindingKind.MapWithoutDefault, FindingSeverity.Warning, map.Tag.Text, null, "the map '" + map.Name + "' has no default row: a value it has no row for prints as it is", map.Location);
                }
            }
        }

        /// <summary>Every setting must be known and every culture a culture; a pattern must be of the family of its setting.</summary>
        private void CheckSettings(IReadOnlyDictionary<string, string> settings)
        {
            foreach (var setting in settings)
            {
                if (!Formatting.IsSetting(setting.Key))
                {
                    this.Add(FindingKind.UnknownSetting, FindingSeverity.Warning, setting.Key, null, "'" + setting.Key + "' is not a document setting and is ignored: the settings are " + string.Join(", ", Formatting.SettingNames), SettingsLocation);
                }
                else if (Formatting.IsCultureSetting(setting.Key))
                {
                    this.CheckCulture(setting.Value, setting.Key, SettingsLocation);
                }
            }

            foreach (var setting in settings)
            {
                var kind = Formatting.KindOfSetting(setting.Key);
                if (string.IsNullOrEmpty(setting.Value) || kind == null)
                {
                    continue;
                }

                var isNumber = kind == ValueKind.Number;
                if (isNumber ? !Patterns.IsNumber(setting.Value) : !Patterns.IsDate(setting.Value))
                {
                    this.Add(FindingKind.BadPattern, FindingSeverity.Error, setting.Key, null, "'" + setting.Value + "' is not a " + (isNumber ? "number" : "date") + " format", SettingsLocation);
                }
            }
        }

        private CultureInfo? CheckCulture(string name, string text, Location location)
        {
            if (name.Length == 0)
            {
                return null;
            }

            var culture = Formatting.ToCulture(name);
            if (culture == null)
            {
                this.Add(FindingKind.BadCulture, FindingSeverity.Error, text, null, "'" + name + "' is not a culture: use a name like nl-BE or en-GB, or invariant", location);
            }

            return culture;
        }

        private void Add(FindingKind kind, FindingSeverity severity, string text, string? path, string message, Location location) =>
            this.findings.Add(new Finding(kind, severity, text, path, message, location));

        private IReadOnlyList<Finding> Sorted() =>
            this.findings.Select((finding, index) => (finding, index))
                .OrderBy(v => PartOrder(v.finding.Location.Part))
                .ThenBy(v => v.finding.Location.Paragraph)
                .ThenBy(v => v.index)
                .Select(v => v.finding)
                .ToList();

        private static int PartOrder(string part)
        {
            switch (part)
            {
                case "content.xml":
                    return 0;
                case "styles.xml":
                    return 1;
                default:
                    return 2;
            }
        }

        private static void CollectText(XmlNode node, StringBuilder text, HashSet<XmlElement> skip)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child is XmlElement element)
                {
                    if (!skip.Contains(element))
                    {
                        CollectText(element, text, skip);
                    }
                }
                else if (child is XmlText || child is XmlWhitespace || child is XmlSignificantWhitespace)
                {
                    text.Append(child.Value);
                }
            }
        }

        /// <summary>A frame of the walk: a loop variable with its shape, or a branch with its narrowings; the root scope has neither.</summary>
        private sealed class Scope
        {
            private readonly List<(object Base, string Prefix, IShape Shape)> narrowings = new List<(object, string, IShape)>();

            public Scope(Scope? parent, string? variable, IShape? variableShape)
            {
                this.Parent = parent;
                this.Variable = variable;
                this.VariableShape = variableShape;
            }

            public Scope? Parent { get; }

            public string? Variable { get; }

            public IShape? VariableShape { get; }

            /// <summary>The innermost loop frame binding this name, or null when the name is a root member.</summary>
            public Scope? Frame(string name)
            {
                for (var scope = this; scope != null; scope = scope.Parent)
                {
                    if (scope.Variable == name)
                    {
                        return scope;
                    }
                }

                return null;
            }

            public void Narrow(object @base, string prefix, IShape shape) => this.narrowings.Add((@base, prefix, shape));

            /// <summary>The narrowed shape of a prefix under a base, from the innermost branch that narrowed it; null when none did.</summary>
            public IShape? Narrowed(object @base, string prefix)
            {
                for (var scope = this; scope != null; scope = scope.Parent)
                {
                    for (var i = scope.narrowings.Count - 1; i >= 0; i--)
                    {
                        var narrowing = scope.narrowings[i];
                        if (ReferenceEquals(narrowing.Base, @base) && narrowing.Prefix == prefix)
                        {
                            return narrowing.Shape;
                        }
                    }
                }

                return null;
            }
        }

        /// <summary>Where a path reached a member only some variants have: the prefix under its base, the variants with the member and those without.</summary>
        private sealed class Narrowing
        {
            public Narrowing(object @base, string prefix, IReadOnlyList<IShape> with, IReadOnlyList<IShape> without, bool isLast)
            {
                this.Base = @base;
                this.Prefix = prefix;
                this.With = with;
                this.Without = without;
                this.IsLast = isLast;
            }

            public object Base { get; }

            public string Prefix { get; }

            public IReadOnlyList<IShape> With { get; }

            public IReadOnlyList<IShape> Without { get; }

            /// <summary>True when the member is the last segment of the path: the test is about this member itself.</summary>
            public bool IsLast { get; }
        }

        /// <summary>The narrowings a guarded walk collects, in path order.</summary>
        private sealed class Split
        {
            public List<Narrowing> Narrowings { get; } = new List<Narrowing>();
        }

        /// <summary>Where a walk over a path failed: the shape it was at, the segment that shape has no member for, and which variants would have had it.</summary>
        private sealed class Failure
        {
            public Failure(IShape at, string segment, IReadOnlyList<IShape> variantsWith, string guard)
            {
                this.At = at;
                this.Segment = segment;
                this.VariantsWith = variantsWith;
                this.Guard = guard;
            }

            public IShape At { get; }

            public string Segment { get; }

            public IReadOnlyList<IShape> VariantsWith { get; }

            /// <summary>The path up to and including the failing segment, as the author would write it in a guard.</summary>
            public string Guard { get; }
        }
    }
}
