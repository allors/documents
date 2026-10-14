// <copyright file="Formatting.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Models
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// The effective formatting at a tag: the culture, the patterns for dates, times, date-times and numbers, the words for booleans,
    /// and the tag's own format. Layers apply from the library's defaults up through the caller, the template's document settings and
    /// the tag's attributes with <see cref="With"/>; the last layer wins.
    /// </summary>
    public sealed class Formatting
    {
        /// <summary>The setting that selects the culture, by name.</summary>
        public const string CultureSetting = "culture";

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

        /// <summary>A tag's own format: a .NET format string, or the name of a pattern setting (date, time, datetime, number).</summary>
        public const string FormatAttribute = "format";

        /// <summary>The names a template's document settings may use.</summary>
        public static readonly IReadOnlyCollection<string> SettingNames = new[] { CultureSetting, DateSetting, TimeSetting, DateTimeSetting, NumberSetting, TrueSetting, FalseSetting };

        /// <summary>The attribute names a binding may use.</summary>
        public static readonly IReadOnlyCollection<string> BindingAttributes = new[] { FormatAttribute, CultureSetting, TrueSetting, FalseSetting };

        /// <summary>The library's defaults: the invariant culture, no patterns, "true" and "false".</summary>
        public static readonly Formatting Default = new Formatting(CultureInfo.InvariantCulture, null, null, null, null, null, "true", "false");

        private Formatting(CultureInfo culture, string format, string date, string time, string dateTime, string number, string trueWord, string falseWord)
        {
            this.Culture = culture;
            this.Format = format;
            this.Date = date;
            this.Time = time;
            this.DateTime = dateTime;
            this.Number = number;
            this.True = trueWord;
            this.False = falseWord;
        }

        public CultureInfo Culture { get; }

        /// <summary>The tag's format attribute; null when the tag has none.</summary>
        public string Format { get; }

        public string Date { get; }

        public string Time { get; }

        public string DateTime { get; }

        public string Number { get; }

        public string True { get; }

        public string False { get; }

        /// <summary>True when the name is a document setting (case-insensitive).</summary>
        public static bool IsSetting(string name) => Contains(SettingNames, name);

        /// <summary>True when the name is an attribute a binding may carry (case-insensitive).</summary>
        public static bool IsBindingAttribute(string name) => Contains(BindingAttributes, name);

        /// <summary>True when the format names a pattern setting instead of being a format string.</summary>
        public static bool IsNamedFormat(string format)
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

        /// <summary>The culture with this name, or null when there is none.</summary>
        public static CultureInfo ToCulture(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
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
        /// A formatting with these settings or attributes applied over this one. Names are case-insensitive; unknown names and
        /// unknown cultures are ignored here, validation reports them. A null or empty value clears the setting.
        /// </summary>
        public Formatting With(IEnumerable<KeyValuePair<string, string>> overrides)
        {
            if (overrides == null)
            {
                return this;
            }

            var culture = this.Culture;
            var format = this.Format;
            var date = this.Date;
            var time = this.Time;
            var dateTime = this.DateTime;
            var number = this.Number;
            var trueWord = this.True;
            var falseWord = this.False;

            foreach (var pair in overrides)
            {
                var value = string.IsNullOrEmpty(pair.Value) ? null : pair.Value;
                switch (pair.Key?.ToLowerInvariant())
                {
                    case CultureSetting:
                        culture = ToCulture(value) ?? culture;
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

            return new Formatting(culture, format, date, time, dateTime, number, trueWord, falseWord);
        }

        /// <summary>A formatting with this culture.</summary>
        public Formatting WithCulture(CultureInfo culture) => new Formatting(culture ?? this.Culture, this.Format, this.Date, this.Time, this.DateTime, this.Number, this.True, this.False);

        /// <summary>
        /// The pattern a value should use: the tag's format when it is a format string, the named setting when the tag names one
        /// (date, time, datetime or number), otherwise the setting the value's kind falls under.
        /// </summary>
        public string Pattern(string settingOfKind)
        {
            switch (this.Format?.ToLowerInvariant())
            {
                case null:
                    return this.Setting(settingOfKind);
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

        private string Setting(string name)
        {
            switch (name)
            {
                case DateSetting:
                    return this.Date;
                case TimeSetting:
                    return this.Time;
                case DateTimeSetting:
                    return this.DateTime;
                case NumberSetting:
                    return this.Number;
                default:
                    return null;
            }
        }
    }
}
