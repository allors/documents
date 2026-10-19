// <copyright file="RenderSettings.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// How a render reads and formats its model: the resolver that binds paths to values, the caller's formatting defaults, the renderers a tag may name and the picture processor.
    /// A template takes its settings when it is read and every render uses them; a render that needs other settings, another culture or a flat
    /// document, comes from another template over the same bytes. The resolver is shared by every render of the template, from any thread.
    /// The template's own document settings win over these, and a tag's attributes win over both; a layer's culture resets the
    /// number and date cultures of the layers below it.
    /// </summary>
    public sealed class RenderSettings
    {
        /// <summary>Binds paths to the model's members; the template's own resolver when null: <see cref="DictionaryResolver"/>, or the reflection resolver of a template over a model type.</summary>
        public IResolver? Resolver { get; init; }

        /// <summary>The culture values print in and text sorts by; the invariant culture when null.</summary>
        public CultureInfo? Culture { get; init; }

        /// <summary>The culture numbers print in when it is not <see cref="Culture"/>; null when they print in the culture.</summary>
        public CultureInfo? NumberCulture { get; init; }

        /// <summary>The culture dates, times and date-times print in when it is not <see cref="Culture"/>; null when they print in the culture.</summary>
        public CultureInfo? DateCulture { get; init; }

        /// <summary>The pattern for a date without a time; the culture's default when null.</summary>
        public string? Date { get; init; }

        /// <summary>The pattern for a time without a date; the culture's default when null.</summary>
        public string? Time { get; init; }

        /// <summary>The pattern for a date with a time; the culture's default when null.</summary>
        public string? DateTime { get; init; }

        /// <summary>The pattern for a number; the culture's default when null.</summary>
        public string? Number { get; init; }

        /// <summary>The word a true value prints; "true" when null.</summary>
        public string? True { get; init; }

        /// <summary>The word a false value prints; "false" when null.</summary>
        public string? False { get; init; }

        /// <summary>
        /// True to render a flat document (.fodt): one XML file with the pictures embedded, which LibreOffice opens as it does a package.
        /// False, the default, renders a package (.odt). Either way the template may be a package or a flat document.
        /// </summary>
        public bool Flat { get; init; }

        /// <summary>
        /// The renderers a tag may name in its format attribute, after the built-in text renderers upper, lower and cap, which every template has:
        /// one at a time or a collection, such as the barcode renderers of a package: <c>Renderers = [new WordsRenderer(), .. BarcodeRenderers.All]</c>.
        /// A later renderer replaces an earlier one with its name, built in or not. A name wins over the pattern names and over a .NET format
        /// string. The template reads them once; a renderer is shared by every render, from any thread, and keeps no state of a render.
        /// </summary>
        public IEnumerable<IRenderer>? Renderers { get; init; }

        /// <summary>
        /// What the package stores for a bound picture: the processor sees the bytes the model or a renderer gave, their format and the frame,
        /// and gives the bytes to store, such as the picture scaled to the frame; null, the default, stores the bytes as they are.
        /// </summary>
        public IPictureProcessor? PictureProcessor { get; init; }

        /// <summary>The caller's layer over the library's defaults.</summary>
        internal Formatting ToFormatting() => Formatting.Default.WithCulture(this.Culture, this.NumberCulture, this.DateCulture).With(this.Settings());

        /// <summary>The settings the caller gave; a setting left null is the library's default, which the layer leaves as it is.</summary>
        private IEnumerable<KeyValuePair<string, string>> Settings()
        {
            foreach (var (name, value) in new[] { (Formatting.DateSetting, this.Date), (Formatting.TimeSetting, this.Time), (Formatting.DateTimeSetting, this.DateTime), (Formatting.NumberSetting, this.Number), (Formatting.TrueSetting, this.True), (Formatting.FalseSetting, this.False) })
            {
                if (value != null)
                {
                    yield return new KeyValuePair<string, string>(name, value);
                }
            }
        }
    }
}
