// <copyright file="TemplateInspection.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// What a template asks of its model: every tag in document order with the paragraph it sits in, the bound picture frames, the maps,
    /// the document settings, and the problems found while reading the tags.
    /// </summary>
    public sealed class TemplateInspection
    {
        internal TemplateInspection(IReadOnlyList<InspectedTag> tags, IReadOnlyList<InspectedPicture> pictures, IReadOnlyList<InspectedMap> maps, IReadOnlyDictionary<string, string> settings, IReadOnlyList<InspectionProblem> problems)
        {
            this.Tags = tags;
            this.Pictures = pictures;
            this.Maps = maps;
            this.Settings = settings;
            this.Problems = problems;
        }

        /// <summary>Every tag of content.xml then styles.xml, in document order. Block tags carry their children too.</summary>
        public IReadOnlyList<InspectedTag> Tags { get; }

        /// <summary>The picture frames whose description is a picture tag.</summary>
        public IReadOnlyList<InspectedPicture> Pictures { get; }

        /// <summary>The maps of the template, in document order: the tables whose first cell is a map tag, content.xml then styles.xml.</summary>
        public IReadOnlyList<InspectedMap> Maps { get; }

        /// <summary>The custom document properties of the template (File, Properties, Custom Properties), by name.</summary>
        public IReadOnlyDictionary<string, string> Settings { get; }

        /// <summary>Tags that could not be read, blocks that do not close or open, else tags that belong to no condition, and maps that cannot be read.</summary>
        public IReadOnlyList<InspectionProblem> Problems { get; }

        /// <summary>Every tag at any depth, in document order.</summary>
        public IEnumerable<InspectedTag> All => Flatten(this.Tags);

        /// <summary>The paths the template reads, deduplicated, loop variables included as the author wrote them.</summary>
        public IReadOnlyList<string> Paths => this.All.Select(v => v.Tag.Path).OfType<string>().Concat(this.Pictures.Select(v => v.Path)).Distinct().ToList();

        public IEnumerable<InspectedTag> Bindings => this.All.Where(v => v.Tag.Kind == TagKind.Binding);

        /// <summary>The for, if and ifdef tags; an else is a child of its condition.</summary>
        public IEnumerable<InspectedTag> Blocks => this.All.Where(v => v.Tag.Kind == TagKind.For || v.Tag.Kind == TagKind.If || v.Tag.Kind == TagKind.IfDef);

        private static IEnumerable<InspectedTag> Flatten(IEnumerable<InspectedTag> tags)
        {
            foreach (var tag in tags)
            {
                yield return tag;

                foreach (var child in Flatten(tag.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
