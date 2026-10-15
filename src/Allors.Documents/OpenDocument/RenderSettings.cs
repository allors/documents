// <copyright file="RenderSettings.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    using System.Collections.Generic;
    using System.Globalization;
    using Allors.Document.Models;

    /// <summary>
    /// How a render reads and formats its model: the resolver that binds paths to values, and the caller's formatting defaults.
    /// The template's own document settings win over these, and a tag's attributes win over both.
    /// </summary>
    public sealed class RenderSettings
    {
        /// <summary>Binds paths to the model's members; the template's own resolver when null: <see cref="DictionaryResolver"/>, or the reflection resolver of a template over a model type.</summary>
        public IResolver Resolver { get; set; }

        /// <summary>The culture dates and numbers print in; the invariant culture when null.</summary>
        public CultureInfo Culture { get; set; }

        /// <summary>The pattern for a date without a time; the culture's default when null.</summary>
        public string Date { get; set; }

        /// <summary>The pattern for a time without a date; the culture's default when null.</summary>
        public string Time { get; set; }

        /// <summary>The pattern for a date with a time; the culture's default when null.</summary>
        public string DateTime { get; set; }

        /// <summary>The pattern for a number; the culture's default when null.</summary>
        public string Number { get; set; }

        /// <summary>The word a true value prints; "true" when null.</summary>
        public string True { get; set; }

        /// <summary>The word a false value prints; "false" when null.</summary>
        public string False { get; set; }

        /// <summary>The caller's layer over the library's defaults.</summary>
        internal Formatting ToFormatting() => Formatting.Default.WithCulture(this.Culture).With(this.Settings());

        private IEnumerable<KeyValuePair<string, string>> Settings()
        {
            yield return new KeyValuePair<string, string>(Formatting.DateSetting, this.Date);
            yield return new KeyValuePair<string, string>(Formatting.TimeSetting, this.Time);
            yield return new KeyValuePair<string, string>(Formatting.DateTimeSetting, this.DateTime);
            yield return new KeyValuePair<string, string>(Formatting.NumberSetting, this.Number);
            yield return new KeyValuePair<string, string>(Formatting.TrueSetting, this.True);
            yield return new KeyValuePair<string, string>(Formatting.FalseSetting, this.False);
        }
    }
}
