// <copyright file="DictionaryShapeTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using Allors.Documents;
    using Allors.Documents.Reflection;
    using Xunit;

    public class DictionaryShapeTests
    {
        [Fact]
        public void AnOpenShapeAcceptsEverythingAndIterates()
        {
            var shape = DictionaryShape.Open;

            Assert.True(shape.IsOpen);
            Assert.Empty(shape.Members);
            Assert.Same(shape, shape.Member("Anything"));
            Assert.Same(shape, shape.Member("Anything").Member("Deeper"));
            Assert.Equal(ValueKind.Open, shape.Kind);
            Assert.Same(shape, shape.Element);
        }

        [Fact]
        public void AClosedShapeKnowsItsMembers()
        {
            var shape = new DictionaryShape(new Dictionary<string, IShape>
            {
                ["Invoice"] = ReflectionShape.Of<Invoice>(),
                ["Issuer"] = DictionaryShape.Open,
            });

            Assert.False(shape.IsOpen);
            Assert.Equal(new[] { "Invoice", "Issuer" }, shape.Members);
            Assert.Equal("Invoice", shape.Member("Invoice").Name);
            Assert.NotNull(shape.Member("Invoice").Member("Number"));
            Assert.Null(shape.Member("Invoice").Member("Nubmer"));
            Assert.Same(DictionaryShape.Open, shape.Member("Issuer"));
            Assert.Null(shape.Member("invoice"));
            Assert.Null(shape.Member("Receiver"));
            Assert.Null(shape.Member(null));
            Assert.Equal(ValueKind.Object, shape.Kind);
            Assert.Null(shape.Element);
        }

        [Fact]
        public void ALeafHasAKindAndNothingElse()
        {
            var shape = new ValueShape(ValueKind.Number, "Amount");

            Assert.Equal(ValueKind.Number, shape.Kind);
            Assert.Equal("Amount", shape.Name);
            Assert.Empty(shape.Members);
            Assert.Null(shape.Member("Anything"));
            Assert.Null(shape.Element);
            Assert.Empty(shape.Variants);
            Assert.Equal("Text", ValueShape.Text.Name);
            Assert.Same(ValueShape.Date, ValueShape.Of(ValueKind.Date));
            Assert.Equal(ValueKind.Picture, ValueShape.Picture.Kind);
        }

        [Theory]
        [InlineData(ValueKind.Open)]
        [InlineData(ValueKind.Object)]
        [InlineData(ValueKind.Collection)]
        public void ALeafIsNeitherOpenNorAnObjectNorACollection(ValueKind kind)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ValueShape(kind));
            Assert.Throws<ArgumentOutOfRangeException>(() => ValueShape.Of(kind));
        }

        [Fact]
        public void AClosedShapeOverLeavesValidatesLikeAClass()
        {
            var invoice = new DictionaryShape(new Dictionary<string, IShape> { ["Number"] = ValueShape.Text, ["Date"] = ValueShape.Date, ["Total"] = ValueShape.Number, ["Logo"] = ValueShape.Picture }, "Invoice");
            var root = new DictionaryShape(new Dictionary<string, IShape> { ["Invoice"] = invoice });
            var body = "<text:p>" + Odt.Placeholder("<$Invoice.Date format=\"N2\">") + "</text:p><text:p>" + Odt.Placeholder("<$Invoice.Total format=\"N2\">") + "</text:p><text:p>" + Odt.Placeholder("<$Invoice.Number.Length>") + "</text:p>";

            var validation = new OpenDocumentTemplate(Odt.Build(body)).Validate(root);

            Assert.Equal(2, validation.Findings.Count);
            Assert.Equal(FindingKind.BadPattern, validation.Findings[0].Kind);
            Assert.Equal(FindingKind.UnknownMember, validation.Findings[1].Kind);
            Assert.Equal("Text has no member Length", validation.Findings[1].Message);
        }

        private sealed class Invoice
        {
            public string Number { get; set; }
        }
    }
}
