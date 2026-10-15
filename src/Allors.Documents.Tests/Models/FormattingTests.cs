// <copyright file="FormattingTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Models.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Allors.Document.Models;
    using Xunit;

    public class FormattingTests
    {
        private static readonly Resolver Resolver = DictionaryResolver.Instance;

        [Fact]
        public void TheDefaultIsInvariantWithoutPatterns()
        {
            var formatting = Formatting.Default;

            Assert.Same(CultureInfo.InvariantCulture, formatting.Culture);
            Assert.Null(formatting.NumberCulture);
            Assert.Null(formatting.DateCulture);
            Assert.Null(formatting.Format);
            Assert.Null(formatting.Date);
            Assert.Null(formatting.DateTime);
            Assert.Null(formatting.Time);
            Assert.Null(formatting.Number);
            Assert.Equal("true", formatting.True);
            Assert.Equal("false", formatting.False);

            Assert.Equal("2.5", Resolver.Format(2.5m, formatting));
            Assert.Equal("10/14/2026 14:30:00", Resolver.Format(new DateTime(2026, 10, 14, 14, 30, 0), formatting));
            Assert.Equal("true", Resolver.Format(true, formatting));
        }

        [Fact]
        public void SettingsLayerOverTheDefaultByNameCaseInsensitively()
        {
            var formatting = Formatting.Default.With(Settings(("Culture", "nl-BE"), ("NUMBER", "N2"), ("date", "dd-MMM-yyyy"), ("datetime", "yyyy-MM-dd HH:mm"), ("true", "Ja"), ("false", "Nee")));

            Assert.Equal("nl-BE", formatting.Culture.Name);
            Assert.Equal("1.234,50", Resolver.Format(1234.5m, formatting));
            Assert.Equal("14-okt-2026", Resolver.Format(new DateTime(2026, 10, 14), formatting));
            Assert.Equal("2026-10-14 14:30", Resolver.Format(new DateTime(2026, 10, 14, 14, 30, 0), formatting));
            Assert.Equal("Ja", Resolver.Format(true, formatting));
            Assert.Equal("Nee", Resolver.Format(false, formatting));
        }

        [Fact]
        public void TheLastLayerWins()
        {
            var template = Formatting.Default.With(Settings(("number", "N2"), ("culture", "nl-BE")));
            var tag = template.With(Settings(("format", "0.000"), ("culture", "en-GB")));

            Assert.Equal("1.234,50", Resolver.Format(1234.5m, template));
            Assert.Equal("1234.500", Resolver.Format(1234.5m, tag));
        }

        [Fact]
        public void ATagFormatWinsOverThePatternOfTheKind()
        {
            var formatting = Formatting.Default.With(Settings(("date", "dd-MMM-yyyy"), ("format", "yyyy")));

            Assert.Equal("2026", Resolver.Format(new DateTime(2026, 10, 14), formatting));
        }

        [Fact]
        public void ANamedFormatSelectsThePatternOfThatName()
        {
            var settings = Formatting.Default.With(Settings(("date", "dd-MMM-yyyy"), ("datetime", "dd-MMM-yyyy HH:mm"), ("number", "N2")));
            var midnight = new DateTime(2026, 10, 14);

            Assert.Equal("14-Oct-2026", Resolver.Format(midnight, settings));
            Assert.Equal("14-Oct-2026 00:00", Resolver.Format(midnight, settings.With(Settings(("format", "datetime")))));
            Assert.Equal("14-Oct-2026", Resolver.Format(new DateTime(2026, 10, 14, 9, 0, 0), settings.With(Settings(("format", "DATE")))));
            Assert.Equal("1,234.50", Resolver.Format(1234.5, settings.With(Settings(("format", "number")))));
        }

        [Fact]
        public void ANamedFormatWithoutThatSettingFallsBackToTheCulture()
        {
            var formatting = Formatting.Default.With(Settings(("format", "datetime")));

            Assert.Equal("10/14/2026 00:00:00", Resolver.Format(new DateTime(2026, 10, 14), formatting));
        }

        [Fact]
        public void ADateAtMidnightIsADateAndOtherwiseADateTime()
        {
            var formatting = Formatting.Default.With(Settings(("date", "yyyy-MM-dd"), ("datetime", "yyyy-MM-dd HH:mm")));

            Assert.Equal("2026-10-14", Resolver.Format(new DateTime(2026, 10, 14), formatting));
            Assert.Equal("2026-10-14 00:01", Resolver.Format(new DateTime(2026, 10, 14, 0, 1, 0), formatting));
            Assert.Equal("2026-10-14", Resolver.Format(new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.FromHours(2)), formatting));
            Assert.Equal("2026-10-14", Resolver.Format(new DateOnly(2026, 10, 14), formatting));
        }

        [Fact]
        public void ATimeUsesTheTimePattern()
        {
            var formatting = Formatting.Default.With(Settings(("time", "HH'h'mm")));

            Assert.Equal("14h30", Resolver.Format(new TimeOnly(14, 30), formatting));
        }

        [Fact]
        public void UnknownNamesAndUnknownCulturesAreIgnored()
        {
            var formatting = Formatting.Default.With(Settings(("colour", "red"), ("culture", "xx-NOPE-zz-123")));

            Assert.Same(CultureInfo.InvariantCulture, formatting.Culture);
        }

        [Fact]
        public void AnEmptyValueClearsASettingAndRestoresTheDefaultWords()
        {
            var formatting = Formatting.Default.With(Settings(("number", "N2"), ("true", "Ja"))).With(Settings(("number", ""), ("true", null)));

            Assert.Null(formatting.Number);
            Assert.Equal("true", formatting.True);
        }

        [Fact]
        public void TheNamesAreKnown()
        {
            Assert.True(Formatting.IsSetting("Culture"));
            Assert.True(Formatting.IsSetting("datetime"));
            Assert.True(Formatting.IsSetting("number-culture"));
            Assert.True(Formatting.IsSetting("Date-Culture"));
            Assert.False(Formatting.IsSetting("format"));
            Assert.True(Formatting.IsCultureSetting("culture"));
            Assert.True(Formatting.IsCultureSetting("NUMBER-CULTURE"));
            Assert.False(Formatting.IsCultureSetting("date"));
            Assert.False(Formatting.IsCultureSetting("my-culture"));
            Assert.True(Formatting.IsBindingAttribute("FORMAT"));
            Assert.True(Formatting.IsBindingAttribute("culture"));
            Assert.True(Formatting.IsBindingAttribute("true"));
            Assert.False(Formatting.IsBindingAttribute("date"));
            Assert.False(Formatting.IsBindingAttribute("number-culture"));
            Assert.True(Formatting.IsNamedFormat("Date"));
            Assert.False(Formatting.IsNamedFormat("yyyy"));
        }

        [Fact]
        public void ADateCultureSetsDatesAndTimesApartFromTheCulture()
        {
            var formatting = Formatting.Default.With(Settings(("culture", "nl-BE"), ("date-culture", "invariant"), ("number", "N2"), ("date", "dd-MMM-yyyy"), ("datetime", "dd-MMM-yyyy HH:mm")));

            Assert.Equal("nl-BE", formatting.Culture.Name);
            Assert.Null(formatting.NumberCulture);
            Assert.Same(CultureInfo.InvariantCulture, formatting.DateCulture);
            Assert.Same(formatting.Culture, formatting.CultureOf(Formatting.NumberSetting));
            Assert.Same(CultureInfo.InvariantCulture, formatting.CultureOf(Formatting.DateSetting));
            Assert.Same(CultureInfo.InvariantCulture, formatting.CultureOf(Formatting.DateTimeSetting));
            Assert.Same(CultureInfo.InvariantCulture, formatting.CultureOf(Formatting.TimeSetting));
            Assert.Same(formatting.Culture, formatting.CultureOf(null));

            Assert.Equal("1.250,50", Resolver.Format(1250.5m, formatting));
            Assert.Equal("14-Sep-2026", Resolver.Format(new DateTime(2026, 9, 14), formatting));
            Assert.Equal("14-Sep-2026 14:30", Resolver.Format(new DateTime(2026, 9, 14, 14, 30, 0), formatting));
            Assert.Equal("14-Sep-2026", Resolver.Format(new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.FromHours(2)), formatting));
            Assert.Equal("14-Sep-2026", Resolver.Format(new DateOnly(2026, 9, 14), formatting));
        }

        [Fact]
        public void ANumberCultureSetsNumbersApartFromTheCulture()
        {
            var formatting = Formatting.Default.With(Settings(("culture", "nl-BE"), ("number-culture", "en-GB"), ("number", "N2"), ("date", "dd-MMM-yyyy")));

            Assert.Equal("en-GB", formatting.NumberCulture.Name);
            Assert.Null(formatting.DateCulture);
            Assert.Equal("1,234.50", Resolver.Format(1234.5m, formatting));
            Assert.Equal("1,234.50", Resolver.Format(1234.5, formatting));
            Assert.Equal("14-okt-2026", Resolver.Format(new DateTime(2026, 10, 14), formatting));
        }

        [Fact]
        public void InvariantNamesTheInvariantCulture()
        {
            Assert.Same(CultureInfo.InvariantCulture, Formatting.ToCulture("invariant"));
            Assert.Same(CultureInfo.InvariantCulture, Formatting.ToCulture("Invariant"));

            var formatting = Formatting.Default.With(Settings(("culture", "nl-BE"))).With(Settings(("culture", "invariant")));

            Assert.Same(CultureInfo.InvariantCulture, formatting.Culture);
        }

        [Fact]
        public void ALayersCultureResetsTheCulturesPerKindOfTheLayersBelow()
        {
            var caller = Formatting.Default.With(Settings(("culture", "nl-BE"), ("number-culture", "en-GB"), ("date-culture", "invariant"), ("number", "N2"), ("date", "dd-MMM-yyyy")));
            var template = caller.With(Settings(("culture", "en-GB")));

            Assert.Null(template.NumberCulture);
            Assert.Null(template.DateCulture);
            Assert.Equal("1,234.50", Resolver.Format(1234.5m, template));
            Assert.Equal("14-Oct-2026", Resolver.Format(new DateTime(2026, 10, 14), template));

            var tag = template.With(Settings(("culture", "nl-BE")));

            Assert.Equal("1.234,50", Resolver.Format(1234.5m, tag));
            Assert.Equal("14-okt-2026", Resolver.Format(new DateTime(2026, 10, 14), tag));
        }

        [Fact]
        public void WithinOneLayerTheCultureOfAKindWinsWhateverTheOrder()
        {
            var lower = Formatting.Default.With(Settings(("culture", "nl-BE"), ("date-culture", "invariant")));
            var dateFirst = lower.With(Settings(("date-culture", "en-GB"), ("culture", "fr-FR")));
            var cultureFirst = lower.With(Settings(("culture", "fr-FR"), ("date-culture", "en-GB")));

            foreach (var formatting in new[] { dateFirst, cultureFirst })
            {
                Assert.Equal("fr-FR", formatting.Culture.Name);
                Assert.Equal("en-GB", formatting.DateCulture.Name);
                Assert.Null(formatting.NumberCulture);
            }
        }

        [Fact]
        public void AnEmptyCultureOfAKindReturnsItToTheCultureAndAnUnknownOneIsIgnored()
        {
            var lower = Formatting.Default.With(Settings(("culture", "nl-BE"), ("date-culture", "invariant"), ("number-culture", "en-GB")));

            var cleared = lower.With(Settings(("date-culture", ""), ("number-culture", null)));

            Assert.Null(cleared.DateCulture);
            Assert.Null(cleared.NumberCulture);
            Assert.Equal("nl-BE", cleared.Culture.Name);

            var unknown = lower.With(Settings(("date-culture", "xx-NOPE"), ("culture", "")));

            Assert.Same(CultureInfo.InvariantCulture, unknown.DateCulture);
            Assert.Equal("en-GB", unknown.NumberCulture.Name);
            Assert.Equal("nl-BE", unknown.Culture.Name);
        }

        [Fact]
        public void WithCultureIsALayerOfItsOwn()
        {
            var nl = CultureInfo.GetCultureInfo("nl-BE");
            var en = CultureInfo.GetCultureInfo("en-GB");

            var layered = Formatting.Default.WithCulture(nl, null, CultureInfo.InvariantCulture);

            Assert.Same(nl, layered.Culture);
            Assert.Null(layered.NumberCulture);
            Assert.Same(CultureInfo.InvariantCulture, layered.DateCulture);

            var kindOnly = layered.WithCulture(null, en, null);

            Assert.Same(nl, kindOnly.Culture);
            Assert.Same(en, kindOnly.NumberCulture);
            Assert.Same(CultureInfo.InvariantCulture, kindOnly.DateCulture);

            var reset = kindOnly.WithCulture(en);

            Assert.Same(en, reset.Culture);
            Assert.Null(reset.NumberCulture);
            Assert.Null(reset.DateCulture);

            var unchanged = kindOnly.WithCulture(null);

            Assert.Same(nl, unchanged.Culture);
            Assert.Same(en, unchanged.NumberCulture);
            Assert.Same(CultureInfo.InvariantCulture, unchanged.DateCulture);
        }

        [Fact]
        public void TextSortsByTheCultureNotByACulturePerKind()
        {
            // Swedish sorts a-umlaut after z; the invariant culture sorts it with a
            var formatting = Formatting.Default.With(Settings(("culture", "sv-SE"), ("number-culture", "invariant"), ("date-culture", "invariant")));

            Assert.True(Resolver.Compare("\u00e4", "z", formatting) > 0);
            Assert.True(Resolver.Compare("\u00e4", "z", Formatting.Default) < 0);
        }

        private static IEnumerable<KeyValuePair<string, string>> Settings(params (string Name, string Value)[] pairs)
        {
            foreach (var (name, value) in pairs)
            {
                yield return new KeyValuePair<string, string>(name, value);
            }
        }
    }
}
