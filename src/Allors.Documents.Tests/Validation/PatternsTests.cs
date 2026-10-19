// <copyright file="PatternsTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Globalization;
    using Allors.Documents;
    using Xunit;

    public class PatternsTests
    {
        [Theory]
        [InlineData("d")]
        [InlineData("D")]
        [InlineData("g")]
        [InlineData("o")]
        [InlineData("yyyy-MM-dd")]
        [InlineData("dd-MMM-yyyy HH:mm")]
        [InlineData("dd/MM/yyyy")]
        [InlineData("HH'h'mm")]
        [InlineData("dd \"at\" HH:mm")]
        [InlineData("HH\\hmm")]
        [InlineData("h:mm tt")]
        [InlineData("yyyy-MM-dd'T'HH:mm:ss.fffK")]
        [InlineData("%d")]
        [InlineData("--")]
        public void ADatePatternIsAStandardLetterOrSpecifierRuns(string pattern)
        {
            Assert.True(Patterns.IsDate(pattern));
            new DateTime(2026, 10, 14, 13, 45, 30).ToString(pattern, CultureInfo.InvariantCulture);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Q")]
        [InlineData("-")]
        [InlineData("N2")]
        [InlineData("yyyy-MM-dd Q")]
        [InlineData("dd at HH:mm")]
        [InlineData("HH'h")]
        [InlineData("HH\\")]
        [InlineData("DateTime")]
        [InlineData("yyyy-MM-ddTHH:mm")]
        public void WhatIsNoDatePatternIsRejected(string pattern) => Assert.False(Patterns.IsDate(pattern));

        [Theory]
        [InlineData("N")]
        [InlineData("N2")]
        [InlineData("F0")]
        [InlineData("C")]
        [InlineData("c2")]
        [InlineData("P1")]
        [InlineData("E3")]
        [InlineData("0")]
        [InlineData("#")]
        [InlineData("0.##")]
        [InlineData("#,##0.00")]
        [InlineData("#,##0.00 'EUR'")]
        [InlineData("#,##0.00;(#,##0.00);-")]
        [InlineData("0 \"pcs\"")]
        [InlineData("0\\%")]
        public void ANumberPatternIsAStandardLetterOrHasAPlaceholder(string pattern)
        {
            Assert.True(Patterns.IsNumber(pattern));
            1234.5m.ToString(pattern, CultureInfo.InvariantCulture);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Q")]
        [InlineData("EUR")]
        [InlineData("yyyy")]
        [InlineData("N2x")]
        [InlineData("0 'pcs")]
        [InlineData("0\\")]
        public void WhatIsNoNumberPatternIsRejected(string pattern) => Assert.False(Patterns.IsNumber(pattern));
    }
}
