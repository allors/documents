// <copyright file="Validator.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using Allors.Document.Inspection;
    using Allors.Document.Models;
    using Allors.Document.Tags;
    using Allors.Document.Xml;
    using Formatting = Allors.Document.Models.Formatting;

    /// <summary>Checks an inspected template against a model shape.</summary>
    internal sealed class Validator
    {
        private static readonly Location SettingsLocation = new Location("meta.xml", 0, string.Empty);

        /// <summary>A loop counter is a number: it has no members and is not a collection.</summary>
        private static readonly IShape CounterShape = new DictionaryShape(new Dictionary<string, IShape>(), "the loop counter");

        private readonly IShape root;
        private readonly List<Finding> findings = new List<Finding>();
        private readonly Dictionary<InspectedTag, Scope> scopeByBranch = new Dictionary<InspectedTag, Scope>();
        private readonly HashSet<XmlElement> blockElements = new HashSet<XmlElement>();
        private readonly HashSet<XmlElement> containersChecked = new HashSet<XmlElement>();
        private readonly Dictionary<string, InspectedMap> mapsByName = new Dictionary<string, InspectedMap>();
        private readonly HashSet<InspectedMap> mapsUsed = new HashSet<InspectedMap>();

        private Validator(IShape root) => this.root = root ?? throw new ArgumentNullException(nameof(root));

        public static TemplateValidation Validate(byte[] package, IShape root)
        {
            var inspection = Inspector.Inspect(package);
            var validator = new Validator(root);
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

        private void CheckPicture(InspectedPicture picture, Scope scope)
        {
            var tag = picture.Tag;
            this.CheckPath(tag.Path, scope, tag.Text, picture.Location);

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
                else if (!TagSyntax.IsEmptyValue(attribute.Value))
                {
                    this.Add(FindingKind.BadValue, FindingSeverity.Error, tag.Text, null, TagSyntax.EmptyAttribute + "=\"" + attribute.Value + "\": use " + TagSyntax.RemoveValue + " or " + TagSyntax.KeepValue, picture.Location);
                }
            }
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
                    if (tag.Tag.Separator != null && pair != null && !BlockStructure.IsInline(pair))
                    {
                        this.Add(FindingKind.SeparatorIgnored, FindingSeverity.Warning, tag.Tag.Text, null, "the separator is not written: it goes between the items of a loop that sits in one paragraph, and this loop's tags sit in " + BlockStructure.Describe(pair.BeginSibling) + "s of their own", tag.Location);
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

            var path = test.Tag.Path;
            var split = new Split();
            var walk = this.Resolve(path, evaluation, split);
            if (walk.Failed)
            {
                this.AddUnknownMember(walk, path, test.Tag.Text, test.Location);
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
            this.CheckPath(tag.Tag.Path, scope, tag.Tag.Text, tag.Location);

            var culture = CultureInfo.InvariantCulture;
            foreach (var attribute in tag.Tag.Attributes)
            {
                if (!TagSyntax.IsBindingAttribute(attribute.Key))
                {
                    this.Add(FindingKind.UnknownAttribute, FindingSeverity.Error, tag.Tag.Text, null, "a binding has no attribute '" + attribute.Key + "': use " + string.Join(", ", TagSyntax.BindingAttributes), tag.Location);
                }
                else if (string.Equals(attribute.Key, Formatting.CultureSetting, StringComparison.OrdinalIgnoreCase))
                {
                    culture = this.CheckCulture(attribute.Value, tag.Tag.Text, tag.Location) ?? culture;
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

            if (tag.Tag.Attributes.TryGetValue(Formatting.FormatAttribute, out var format) && !Formatting.IsNamedFormat(format) && !IsDatePattern(format, culture) && !IsNumberPattern(format, culture))
            {
                this.Add(FindingKind.BadPattern, FindingSeverity.Error, tag.Tag.Text, null, "'" + format + "' is not a format: use a .NET format string, or date, time, datetime or number for the document's pattern", tag.Location);
            }
        }

        private Scope CheckLoop(InspectedTag tag, Scope scope)
        {
            var variable = tag.Tag.Variable;
            var walk = this.Resolve(tag.Tag.Path, scope, null);
            IShape variableShape;
            if (walk.Failed)
            {
                this.AddUnknownMember(walk, tag.Tag.Path, tag.Tag.Text, tag.Location);
                variableShape = DictionaryShape.Open;
            }
            else if (walk.Shape.IsCollection)
            {
                variableShape = walk.Shape.Element ?? DictionaryShape.Open;
            }
            else
            {
                this.Add(FindingKind.NotACollection, FindingSeverity.Warning, tag.Tag.Text, tag.Tag.Path, "'" + tag.Tag.Path + "' is " + Describe(walk.Shape) + ", not a collection: the loop runs once over the value", tag.Location);
                variableShape = walk.Shape;
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
                var keyWalk = Walk.Relative(variableShape, key.Path);
                if (keyWalk.Failed)
                {
                    this.Add(FindingKind.UnknownMember, FindingSeverity.Error, tag.Tag.Text, key.Path, "sort key '" + key.Path + "': " + UnknownMemberText(keyWalk), tag.Location);
                }
            }

            var body = new Scope(scope, variable, variableShape);
            if (tag.Tag.Index == null)
            {
                return body;
            }

            return new Scope(body, tag.Tag.Index, CounterShape);
        }

        private void CheckPath(string path, Scope scope, string text, Location location)
        {
            var walk = this.Resolve(path, scope, null);
            if (walk.Failed)
            {
                this.AddUnknownMember(walk, path, text, location);
            }
        }

        private void AddUnknownMember(Walk walk, string path, string text, Location location)
        {
            var message = UnknownMemberText(walk);
            if (walk.VariantsWith.Count > 0)
            {
                var guard = walk.Guard;
                message += ": put the block in <@ifdef " + guard + "> or <@if " + guard + ">";
            }

            this.Add(FindingKind.UnknownMember, FindingSeverity.Error, text, path, message, location);
        }

        private static string UnknownMemberText(Walk walk)
        {
            if (walk.VariantsWith.Count > 0)
            {
                return walk.Segment + " is a member of " + string.Join(" and ", walk.VariantsWith.Select(v => v.Name)) + " but not of every " + walk.At.Name;
            }

            return Describe(walk.At) + " has no member " + walk.Segment;
        }

        private static string Describe(IShape shape) => shape is DictionaryShape dictionary && dictionary.IsOpen ? "an open value" : shape.Name;

        /// <summary>
        /// Walks a path from a scope: the first segment is a loop variable in scope, innermost first, or a member of the root; the rest are members.
        /// With a split, a segment that only some variants have is noted in it and the walk goes on over those variants instead of failing.
        /// </summary>
        private Walk Resolve(string path, Scope scope, Split split)
        {
            var segments = path.Split('.');
            var frame = scope.Frame(segments[0]);
            var @base = (object)frame ?? this.root;
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
                        return Walk.Failure(current, segment, with, string.Join(".", segments.Take(i + 1)));
                    }

                    var without = current.Variants.Where(v => v.Member(segment) == null).ToList();
                    split.Narrowings.Add(new Narrowing(@base, prefix, with, without, i == segments.Length - 1));
                    member = Shape(with).Member(segment);
                }

                prefix = prefix.Length == 0 ? segment : prefix + "." + segment;
                current = scope.Narrowed(@base, prefix) ?? member;
            }

            return Walk.Success(current);
        }

        /// <summary>
        /// What the renderer does with a block: it takes the lowest common ancestor of its tags and removes the children of that ancestor that
        /// hold them, keeping or repeating what sits between. Tags in one paragraph cost only their text runs; tags in two cells of one row cost
        /// the cells; a tag inside a table whose partner is outside costs the whole table. The block's pair, for what else depends on its shape; null when it has none.
        /// </summary>
        private XmlPair CheckStructure(InspectedTag block)
        {
            var end = block.End;
            if (end == null || block.Element == null || end.Element == null)
            {
                return null;
            }

            var pair = new XmlPair(null, block.Element) { End = end.Element };
            var elses = block.Elses.Where(v => v.Element != null).ToList();
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
                this.CheckMixedText(pair.SeparatorSiblings[i], elses[i]);
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

        /// <summary>Every setting must be known and every culture a culture; a pattern is tried in the culture its kind prints in.</summary>
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

            var formatting = Formatting.Default.With(settings);
            foreach (var setting in settings)
            {
                var name = setting.Key.ToLowerInvariant();
                var isDate = name == Formatting.DateSetting || name == Formatting.TimeSetting || name == Formatting.DateTimeSetting;
                var isNumber = name == Formatting.NumberSetting;
                if (string.IsNullOrEmpty(setting.Value) || (!isDate && !isNumber))
                {
                    continue;
                }

                var culture = formatting.CultureOf(name);
                if (isDate ? !IsDatePattern(setting.Value, culture) : !IsNumberPattern(setting.Value, culture))
                {
                    this.Add(FindingKind.BadPattern, FindingSeverity.Error, setting.Key, null, "'" + setting.Value + "' is not a " + (isDate ? "date" : "number") + " format", SettingsLocation);
                }
            }
        }

        private CultureInfo CheckCulture(string name, string text, Location location)
        {
            if (string.IsNullOrEmpty(name))
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

        private void Add(FindingKind kind, FindingSeverity severity, string text, string path, string message, Location location) =>
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

        private static bool IsDatePattern(string pattern, CultureInfo culture)
        {
            try
            {
                new DateTime(2026, 10, 14, 13, 45, 30).ToString(pattern, culture);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsNumberPattern(string pattern, CultureInfo culture)
        {
            try
            {
                1234.5m.ToString(pattern, culture);
                return true;
            }
            catch (FormatException)
            {
                return false;
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

            public Scope(Scope parent, string variable, IShape variableShape)
            {
                this.Parent = parent;
                this.Variable = variable;
                this.VariableShape = variableShape;
            }

            public Scope Parent { get; }

            public string Variable { get; }

            public IShape VariableShape { get; }

            /// <summary>The innermost loop frame binding this name, or null when the name is a root member.</summary>
            public Scope Frame(string name)
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
            public IShape Narrowed(object @base, string prefix)
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

        /// <summary>The outcome of walking a path: the shape it reaches, or where it failed and which variants would have had the member.</summary>
        private sealed class Walk
        {
            private Walk(IShape shape, IShape at, string segment, IReadOnlyList<IShape> variantsWith, string guard)
            {
                this.Shape = shape;
                this.At = at;
                this.Segment = segment;
                this.VariantsWith = variantsWith;
                this.Guard = guard;
            }

            public bool Failed => this.Shape == null;

            public IShape Shape { get; }

            public IShape At { get; }

            public string Segment { get; }

            public IReadOnlyList<IShape> VariantsWith { get; }

            /// <summary>The path up to and including the failing segment, as the author would write it in a guard.</summary>
            public string Guard { get; }

            public static Walk Success(IShape shape) => new Walk(shape, null, null, Array.Empty<IShape>(), null);

            public static Walk Failure(IShape at, string segment, IReadOnlyList<IShape> variantsWith, string guard) => new Walk(null, at, segment, variantsWith, guard);

            /// <summary>Walks a path over a shape alone, as a sort key is relative to the item.</summary>
            public static Walk Relative(IShape shape, string path)
            {
                var current = shape;
                var segments = path.Split('.');
                for (var i = 0; i < segments.Length; i++)
                {
                    var member = current.Member(segments[i]);
                    if (member == null)
                    {
                        return Failure(current, segments[i], current.Variants.Where(v => v.Member(segments[i]) != null).ToList(), string.Join(".", segments.Take(i + 1)));
                    }

                    current = member;
                }

                return Success(current);
            }
        }
    }
}
