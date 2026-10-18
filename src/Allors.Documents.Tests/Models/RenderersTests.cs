// <copyright file="RenderersTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Models.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Allors.Document.Models;
    using Allors.Document.OpenDocument;
    using Xunit;

    /// <summary>The registry of renderers, and the built-in text renderers every registry starts with.</summary>
    public class RenderersTests
    {
        [Fact]
        public void ARegistryStartsWithTheBuiltInTextRenderers()
        {
            var renderers = new Renderers();

            Assert.Equal(["upper", "lower", "cap"], renderers.Names);
            Assert.Equal(3, renderers.Count);
            Assert.Equal(new IRenderer[] { TextRenderers.Upper, TextRenderers.Lower, TextRenderers.Cap }, renderers);
            Assert.True(renderers.TryGet("upper", out var upper));
            Assert.Same(TextRenderers.Upper, upper);
        }

        [Theory]
        [InlineData("UPPER")]
        [InlineData(" upper ")]
        [InlineData("Upper")]
        public void ANameIsLookedUpInAnyCaseWithWhiteSpaceAroundItIgnored(string name)
        {
            Assert.True(new Renderers().TryGet(name, out var renderer));
            Assert.Same(TextRenderers.Upper, renderer);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("nope")]
        public void ANameNoRendererHasIsNotFound(string name)
        {
            Assert.False(new Renderers().TryGet(name, out var renderer));
            Assert.Null(renderer);
        }

        [Fact]
        public void ARendererIsRegisteredUnderItsNameAfterTheBuiltIns()
        {
            var words = new Named("words");
            var renderers = new Renderers { words };

            Assert.Equal(["upper", "lower", "cap", "words"], renderers.Names);
            Assert.True(renderers.TryGet("WORDS", out var found));
            Assert.Same(words, found);
        }

        [Fact]
        public void ARendererWithAKnownNameReplacesTheOtherInItsPlace()
        {
            var shout = new Named("Upper");
            var renderers = new Renderers { shout };

            Assert.Equal(["Upper", "lower", "cap"], renderers.Names);
            Assert.Equal(3, renderers.Count);
            Assert.True(renderers.TryGet("upper", out var found));
            Assert.Same(shout, found);
        }

        [Fact]
        public void ACollectionInitialiserTakesRenderersOneByOneOrAsCollections()
        {
            var a = new Named("a");
            var b = new Named("b");
            var c = new Named("c");

            var settings = new RenderSettings { Renderers = { a, new IRenderer[] { b, c } } };

            Assert.Equal(["upper", "lower", "cap", "a", "b", "c"], settings.Renderers.Names);
        }

        [Fact]
        public void ARendererWithoutANameIsRefused()
        {
            var renderers = new Renderers();

            Assert.Throws<ArgumentNullException>(() => renderers.Add((IRenderer)null));
            Assert.Throws<ArgumentNullException>(() => renderers.Add((IEnumerable<IRenderer>)null));
            Assert.Throws<ArgumentException>(() => renderers.Add(new Named(null)));
            Assert.Throws<ArgumentException>(() => renderers.Add(new Named(" ")));
            Assert.Equal(3, renderers.Count);
        }

        [Fact]
        public void ANameIsRegisteredTrimmed()
        {
            var renderers = new Renderers { new Named(" words ") };

            Assert.True(renderers.TryGet("words", out _));
        }

        [Fact]
        public void TheRenderSettingsStartWithARegistryAndWithoutAProcessor()
        {
            var settings = new RenderSettings();

            Assert.Equal(["upper", "lower", "cap"], settings.Renderers.Names);
            Assert.Null(settings.PictureProcessor);
        }

        [Theory]
        [InlineData("upper", "van Exem bvba", "VAN EXEM BVBA")]
        [InlineData("lower", "Van Exem BVBA", "van exem bvba")]
        [InlineData("cap", "van Exem bvba", "Van Exem bvba")]
        [InlineData("cap", "", "")]
        [InlineData("cap", "ñandú", "Ñandú")]
        [InlineData("upper", "élan", "ÉLAN")]
        public void TheBuiltInsTransformTheText(string name, string text, string expected)
        {
            Assert.True(new Renderers().TryGet(name, out var renderer));

            Assert.Equal(expected, ((ITextRenderer)renderer).Render(text, text, Formatting.Default));
        }

        [Fact]
        public void UpperAndLowerFollowTheCultureOfTheFormatting()
        {
            var turkish = Formatting.Default.WithCulture(CultureInfo.GetCultureInfo("tr-TR"));

            Assert.Equal("İSTANBUL", TextRenderers.Upper.Render("istanbul", "istanbul", turkish));
            Assert.Equal("ISTANBUL", TextRenderers.Upper.Render("istanbul", "istanbul", Formatting.Default));
            Assert.Equal("ıstanbul", TextRenderers.Lower.Render("ISTANBUL", "ISTANBUL", turkish));
        }

        [Fact]
        public void NullTextStaysNullAndANullFormattingMeansTheDefault()
        {
            Assert.Null(TextRenderers.Upper.Render("x", null, Formatting.Default));
            Assert.Equal("X", TextRenderers.Upper.Render("x", "x", null));
        }

        [Fact]
        public void TheBuiltInsAreListedInOrder() => Assert.Equal(["upper", "lower", "cap"], TextRenderers.All.Select(v => v.Name));

        private sealed class Named : IRenderer
        {
            public Named(string name) => this.Name = name;

            public string Name { get; }
        }
    }
}
