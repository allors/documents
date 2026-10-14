// <copyright file="DictionaryShapeTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Models.Tests
{
    using System.Collections.Generic;
    using Allors.Document.Models;
    using Allors.Document.Reflection;
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
            Assert.True(shape.IsCollection);
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
            Assert.False(shape.IsCollection);
            Assert.Null(shape.Element);
        }

        private sealed class Invoice
        {
            public string Number { get; set; }
        }
    }
}
