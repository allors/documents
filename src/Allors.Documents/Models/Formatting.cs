// <copyright file="Formatting.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// The effective formatting at a tag: the cultures, the patterns for dates, times, date-times and numbers, the words for booleans,
    /// and the tag's own format. Layers apply from the library's defaults up through the caller, the template's document settings and
    /// the tag's attributes with <see cref="With"/>; the last layer wins. Numbers and dates may print in a culture of their own next to
    /// the culture. The layers combine those as style sheets do: a layer's culture resets the cultures per kind of the layers below it,
    /// and within one layer the culture of a kind wins over the culture, whatever the order.
    /// </summary>
    public sealed class Formatting
    {
        /// <summary>The setting that selects the culture, by name.</summary>
        public const string CultureSetting = "culture";

        /// <summary>The setting that selects the culture numbers print in, by name, apart from the culture.</summary>
        public const string NumberCultureSetting = "number-culture";

        /// <summary>The setting that selects the culture dates, times and date-times print in, by name, apart from the culture.</summary>
        public const string DateCultureSetting = "date-culture";

        /// <summary>The name of the invariant culture in a setting or an attribute: its names and separators are the same on every machine.</summary>
        public const string InvariantCultureName = "invariant";

        /// <summary>The pattern for a date without a time.</summary>
        public const string DateSetting = "date";

        /// <summary>The pattern for a time without a date.</summary>
        public const string TimeSetting = "time";

        /// <summary>The pattern for a date with a time.</summary>
        public const string DateTimeSetting = "datetime";

        /// <summary>The pattern for a number.</summary>
        public const string NumberSetting = "number";

        /// <summary>The word for true.</summary>
        public const string TrueSetting = "true";

        /// <summary>The word for false.</summary>
        public const string FalseSetting = "false";

        /// <summary>
        /// A tag's own format: the name of a renderer, the name of a pattern setting (date, time, datetime, number) or a .NET format string, looked up
        /// in that order; on a picture tag, the name of a picture renderer. Here it is the pattern or format string: a renderer's name is taken out before the resolver formats.
        /// </summary>
        public const string FormatAttribute = "format";

        /// <summary>The names a template's document settings may use.</summary>
        public static readonly IReadOnlyCollection<string> SettingNames = new[] { CultureSetting, NumberCultureSetting, DateCultureSetting, DateSetting, TimeSetting, DateTimeSetting, NumberSetting, TrueSetting, FalseSetting };

        /// <summary>The attribute names a binding may use.</summary>
        public static readonly IReadOnlyCollection<string> BindingAttributes = new[] { FormatAttribute, CultureSetting, TrueSetting, FalseSetting };

        /// <summary>The library's defaults: the invariant culture for every kind, no patterns, "true" and "false".</summary>
        public static readonly Formatting Default = new Formatting(CultureInfo.InvariantCulture, null, null, null, null, null, null, null, "true", "false");

        private Formatting(CultureInfo culture, CultureInfo? numberCulture, CultureInfo? dateCulture, string? format, string? date, string? time, string? dateTime, string? number, string trueWord, string falseWord)
        {
            this.Culture = culture;
            this.NumberCulture = numberCulture;
            this.DateCulture = dateCulture;
            this.Format = format;
            this.Date = date;
            this.Time = time;
            this.DateTime = dateTime;
            this.Number = number;
            this.True = trueWord;
            this.False = falseWord;
        }

        /// <summary>The culture: what every kind prints in unless it has a culture of its own, and what text sorts by.</summary>
        public CultureInfo Culture { get; }

        /// <summary>The culture numbers print in when they have one of their own; null when they print in <see cref="Culture"/>.</summary>
        public CultureInfo? NumberCulture { get; }

        /// <summary>The culture dates, times and date-times print in when they have one of their own; null when they print in <see cref="Culture"/>.</summary>
        public CultureInfo? DateCulture { get; }

        /// <summary>The tag's format attribute; null when the tag has none.</summary>
        public string? Format { get; }

        /// <summary>The pattern for a date without a time; null for the culture's default.</summary>
        public string? Date { get; }

        /// <summary>The pattern for a time without a date; null for the culture's default.</summary>
        public string? Time { get; }

        /// <summary>The pattern for a date with a time; null for the culture's default.</summary>
        public string? DateTime { get; }

        /// <summary>The pattern for a number; null for the culture's default.</summary>
        public string? Number { get; }

        public string True { get; }

        public string False { get; }

        /// <summary>True when the name is a document setting (case-insensitive).</summary>
        public static bool IsSetting(string name) => Contains(SettingNames, name);

        /// <summary>True when the name is a document setting that selects a culture (case-insensitive): culture, number-culture or date-culture.</summary>
        public static bool IsCultureSetting(string name) => IsSetting(name) && name.EndsWith(CultureSetting, StringComparison.OrdinalIgnoreCase);

        /// <summary>True when the name is an attribute a binding may carry (case-insensitive).</summary>
        public static bool IsBindingAttribute(string name) => Contains(BindingAttributes, name);

        /// <summary>True when the format names a pattern setting instead of being a format string.</summary>
        public static bool IsNamedFormat(string? format)
        {
            switch (format?.ToLowerInvariant())
            {
                case DateSetting:
                case TimeSetting:
                case DateTimeSetting:
                case NumberSetting:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The culture with this name, the invariant culture for "invariant", or null when there is none.</summary>
        public static CultureInfo? ToCulture(string? name)
        {
            if (name == null || name.Length == 0)
            {
                return null;
            }

            if (string.Equals(name, InvariantCultureName, StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.InvariantCulture;
            }

            try
            {
#if NET
                return CultureInfo.GetCultureInfo(name, predefinedOnly: true);
#else
                return CultureInfo.GetCultureInfo(name);
#endif
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// A formatting with these settings or attributes applied over this one as one layer. Names are case-insensitive; unknown names and
        /// unknown cultures are ignored here, validation reports them. A null or empty value clears the setting: a pattern goes, a word returns
        /// to its default, the culture of a kind returns to the culture; an empty culture changes nothing. The layer's culture resets the
        /// cultures per kind of the layers below before the layer's own cultures per kind apply, so their order in the layer does not matter.
        /// </summary>
        public Formatting With(IEnumerable<KeyValuePair<string, string>> overrides)
        {
            if (overrides == null)
            {
                return this;
            }

            var culture = this.Culture;
            var numberCulture = this.NumberCulture;
            var dateCulture = this.DateCulture;
            var format = this.Format;
            var date = this.Date;
            var time = this.Time;
            var dateTime = this.DateTime;
            var number = this.Number;
            var trueWord = this.True;
            var falseWord = this.False;

            var hasNumberCulture = false;
            var hasDateCulture = false;
            string? numberCultureName = null;
            string? dateCultureName = null;

            foreach (var pair in overrides)
            {
                var value = string.IsNullOrEmpty(pair.Value) ? null : pair.Value;
                switch (pair.Key?.ToLowerInvariant())
                {
                    case CultureSetting:
                        var layerCulture = ToCulture(value);
                        if (layerCulture != null)
                        {
                            culture = layerCulture;
                            numberCulture = null;
                            dateCulture = null;
                        }

                        break;
                    case NumberCultureSetting:
                        hasNumberCulture = true;
                        numberCultureName = value;
                        break;
                    case DateCultureSetting:
                        hasDateCulture = true;
                        dateCultureName = value;
                        break;
                    case FormatAttribute:
                        format = value;
                        break;
                    case DateSetting:
                        date = value;
                        break;
                    case TimeSetting:
                        time = value;
                        break;
                    case DateTimeSetting:
                        dateTime = value;
                        break;
                    case NumberSetting:
                        number = value;
                        break;
                    case TrueSetting:
                        trueWord = value ?? Default.True;
                        break;
                    case FalseSetting:
                        falseWord = value ?? Default.False;
                        break;
                }
            }

            if (hasNumberCulture)
            {
                numberCulture = numberCultureName == null ? null : ToCulture(numberCultureName) ?? numberCulture;
            }

            if (hasDateCulture)
            {
                dateCulture = dateCultureName == null ? null : ToCulture(dateCultureName) ?? dateCulture;
            }

            return new Formatting(culture, numberCulture, dateCulture, format, date, time, dateTime, number, trueWord, falseWord);
        }

        /// <summary>A formatting with this culture as a layer of its own: every kind prints in it, the cultures per kind of the layers below reset.</summary>
        public Formatting WithCulture(CultureInfo culture) => this.WithCulture(culture, null, null);

        /// <summary>
        /// A formatting with these cultures as one layer: the culture, when given, resets the cultures per kind of the layers below it;
        /// the number culture and the date culture, when given, set their kind apart from the culture.
        /// </summary>
        public Formatting WithCulture(CultureInfo? culture, CultureInfo? numberCulture, CultureInfo? dateCulture)
        {
            var reset = culture != null;
            return new Formatting(
                culture ?? this.Culture,
                numberCulture ?? (reset ? null : this.NumberCulture),
                dateCulture ?? (reset ? null : this.DateCulture),
                this.Format,
                this.Date,
                this.Time,
                this.DateTime,
                this.Number,
                this.True,
                this.False);
        }

        /// <summary>
        /// The culture a value of this kind prints in: the number culture for a number, the date culture for a date, a time or a date-time,
        /// each when it has one of its own, and otherwise the culture.
        /// </summary>
        public CultureInfo CultureFor(ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.Number:
                    return this.NumberCulture ?? this.Culture;
                case ValueKind.Date:
                case ValueKind.Time:
                case ValueKind.DateTime:
                    return this.DateCulture ?? this.Culture;
                default:
                    return this.Culture;
            }
        }

        /// <summary>
        /// The pattern a value of this kind prints with: the tag's format when it is a format string, the named setting when the tag names one
        /// (date, time, datetime or number), otherwise the setting of the kind: date, time, datetime or number, and none for the other kinds.
        /// </summary>
        public string? PatternFor(ValueKind kind)
        {
            switch (this.Format?.ToLowerInvariant())
            {
                case null:
                    return this.SettingFor(kind);
                case DateSetting:
                    return this.Date;
                case TimeSetting:
                    return this.Time;
                case DateTimeSetting:
                    return this.DateTime;
                case NumberSetting:
                    return this.Number;
                default:
                    return this.Format;
            }
        }

        /// <summary>The kind a pattern setting formats: date, time, datetime or number; null for another name.</summary>
        public static ValueKind? KindOfSetting(string? name)
        {
            switch (name?.ToLowerInvariant())
            {
                case DateSetting:
                    return ValueKind.Date;
                case TimeSetting:
                    return ValueKind.Time;
                case DateTimeSetting:
                    return ValueKind.DateTime;
                case NumberSetting:
                    return ValueKind.Number;
                default:
                    return null;
            }
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

        private string? SettingFor(ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.Date:
                    return this.Date;
                case ValueKind.Time:
                    return this.Time;
                case ValueKind.DateTime:
                    return this.DateTime;
                case ValueKind.Number:
                    return this.Number;
                default:
                    return null;
            }
        }
    }
}
