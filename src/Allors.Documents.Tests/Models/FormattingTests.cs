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
            Assert.False(Formatting.IsSetting("format"));
            Assert.True(Formatting.IsBindingAttribute("FORMAT"));
            Assert.True(Formatting.IsBindingAttribute("culture"));
            Assert.True(Formatting.IsBindingAttribute("true"));
            Assert.False(Formatting.IsBindingAttribute("date"));
            Assert.True(Formatting.IsNamedFormat("Date"));
            Assert.False(Formatting.IsNamedFormat("yyyy"));
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
