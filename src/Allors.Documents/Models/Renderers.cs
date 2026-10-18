// <copyright file="Renderers.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Models
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The renderers a template may name in a format attribute, by name in any case: the built-in text renderers <c>upper</c>, <c>lower</c> and
    /// <c>cap</c>, and what the caller registers, one at a time or as a collection: <c>new RenderSettings { Renderers = { BarcodeRenderers.All, new WordsRenderer() } }</c>.
    /// A renderer added under a name the registry has replaces the one it had, built in or not. A registered name wins over the pattern names
    /// date, time, datetime and number and over a .NET format string. Register before rendering; one registry may serve every render and Validate.
    /// </summary>
    public sealed class Renderers : IEnumerable<IRenderer>
    {
        private readonly List<IRenderer> ordered = new List<IRenderer>();
        private readonly Dictionary<string, IRenderer> byName = new Dictionary<string, IRenderer>(StringComparer.OrdinalIgnoreCase);

        /// <summary>A registry with the built-in text renderers.</summary>
        public Renderers()
        {
            foreach (var renderer in TextRenderers.All)
            {
                this.Add(renderer);
            }
        }

        /// <summary>The names of the renderers, in registration order; a replaced renderer keeps its place.</summary>
        public IEnumerable<string> Names => this.ordered.Select(v => v.Name);

        public int Count => this.ordered.Count;

        /// <summary>The names of the text renderers for a message: "upper, lower or cap"; empty when there are none.</summary>
        internal string TextNames => Join(this.ordered.OfType<ITextRenderer>().Select(v => v.Name));

        /// <summary>The names of the picture renderers for a message: "code128, ean13 or qr"; empty when there are none.</summary>
        internal string PictureNames => Join(this.ordered.OfType<IPictureRenderer>().Select(v => v.Name));

        /// <summary>Registers a renderer under its name; the renderer the registry had under that name, if any, is replaced in place.</summary>
        public void Add(IRenderer renderer)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            var name = renderer.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("a renderer needs a name: the text a tag selects it by in its format attribute", nameof(renderer));
            }

            if (this.byName.TryGetValue(name, out var existing))
            {
                this.ordered[this.ordered.IndexOf(existing)] = renderer;
            }
            else
            {
                this.ordered.Add(renderer);
            }

            this.byName[name] = renderer;
        }

        /// <summary>Registers every renderer of the collection, in its order.</summary>
        public void Add(IEnumerable<IRenderer> renderers)
        {
            if (renderers == null)
            {
                throw new ArgumentNullException(nameof(renderers));
            }

            foreach (var renderer in renderers)
            {
                this.Add(renderer);
            }
        }

        /// <summary>The renderer registered under this name, in any case and with white space around it ignored; false when there is none.</summary>
        public bool TryGet(string name, out IRenderer renderer)
        {
            var key = name?.Trim();
            if (string.IsNullOrEmpty(key))
            {
                renderer = null;
                return false;
            }

            return this.byName.TryGetValue(key, out renderer);
        }

        public IEnumerator<IRenderer> GetEnumerator() => this.ordered.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        /// <summary>Names as a message lists them: "a", "a or b", "a, b or c".</summary>
        private static string Join(IEnumerable<string> names)
        {
            var list = names.ToList();
            switch (list.Count)
            {
                case 0:
                    return string.Empty;
                case 1:
                    return list[0];
                default:
                    return string.Join(", ", list.Take(list.Count - 1)) + " or " + list[list.Count - 1];
            }
        }
    }
}
