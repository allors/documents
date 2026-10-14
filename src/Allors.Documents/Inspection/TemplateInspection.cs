// <copyright file="TemplateInspection.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Inspection
{
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Document.Tags;

    /// <summary>
    /// What a template asks of its model: every tag in document order with the paragraph it sits in, the bound picture frames,
    /// the document settings, and the problems found while reading the tags.
    /// </summary>
    public sealed class TemplateInspection
    {
        internal TemplateInspection(IReadOnlyList<InspectedTag> tags, IReadOnlyList<InspectedPicture> pictures, IReadOnlyDictionary<string, string> settings, IReadOnlyList<InspectionProblem> problems)
        {
            this.Tags = tags;
            this.Pictures = pictures;
            this.Settings = settings;
            this.Problems = problems;
        }

        /// <summary>Every tag of content.xml then styles.xml, in document order. Block tags carry their children too.</summary>
        public IReadOnlyList<InspectedTag> Tags { get; }

        /// <summary>The picture frames whose name starts with $.</summary>
        public IReadOnlyList<InspectedPicture> Pictures { get; }

        /// <summary>The custom document properties of the template (File, Properties, Custom Properties), by name.</summary>
        public IReadOnlyDictionary<string, string> Settings { get; }

        /// <summary>Tags that could not be read and blocks that do not close or open.</summary>
        public IReadOnlyList<InspectionProblem> Problems { get; }

        /// <summary>Every tag at any depth, in document order.</summary>
        public IEnumerable<InspectedTag> All => Flatten(this.Tags);

        /// <summary>The paths the template reads, deduplicated, loop variables included as the author wrote them.</summary>
        public IReadOnlyList<string> Paths => this.All.Where(v => v.Tag.Path != null).Select(v => v.Tag.Path).Concat(this.Pictures.Select(v => v.Path)).Distinct().ToList();

        public IEnumerable<InspectedTag> Bindings => this.All.Where(v => v.Tag.Kind == TagKind.Binding);

        public IEnumerable<InspectedTag> Blocks => this.All.Where(v => v.Tag.Kind == TagKind.For || v.Tag.Kind == TagKind.If);

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
