// <copyright file="Renderers.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The renderers of a template by name, frozen when the template is read: the built-in text renderers first, then the settings' in their
    /// order, where a later renderer replaces an earlier one with its name, built in or not, and keeps its place. A name is looked up in any case,
    /// with white space around it ignored. A template's renderers never change, so every render and Validate share them.
    /// </summary>
    internal sealed class Renderers : IEnumerable<IRenderer>
    {
        /// <summary>The built-in renderers alone, for a template whose settings list none.</summary>
        public static readonly Renderers Default = new Renderers(null);

        private readonly List<IRenderer> ordered = new List<IRenderer>();
        private readonly Dictionary<string, IRenderer> byName = new Dictionary<string, IRenderer>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The built-ins and the settings' renderers; a null renderer or one without a name is an argument error.</summary>
        public Renderers(IEnumerable<IRenderer> renderers)
        {
            foreach (var renderer in TextRenderers.All)
            {
                this.Add(renderer);
            }

            if (renderers == null)
            {
                return;
            }

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    throw new ArgumentException("the renderers hold a null: list renderers only", nameof(renderers));
                }

                if (string.IsNullOrWhiteSpace(renderer.Name))
                {
                    throw new ArgumentException("a renderer needs a name, the text a tag selects it by in its format attribute: " + renderer.GetType().Name + " has none", nameof(renderers));
                }

                this.Add(renderer);
            }
        }

        /// <summary>The names of the renderers, in order.</summary>
        public IEnumerable<string> Names => this.ordered.Select(v => v.Name);

        public int Count => this.ordered.Count;

        /// <summary>The names of the text renderers for a message: "upper, lower or cap"; empty when there are none.</summary>
        public string TextNames => Join(this.ordered.OfType<ITextRenderer>().Select(v => v.Name));

        /// <summary>The names of the picture renderers for a message: "code128, ean13 or qr"; empty when there are none.</summary>
        public string PictureNames => Join(this.ordered.OfType<IPictureRenderer>().Select(v => v.Name));

        /// <summary>The renderer under this name, in any case and with white space around it ignored; false when there is none.</summary>
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

        private void Add(IRenderer renderer)
        {
            var name = renderer.Name.Trim();
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
    }
}
