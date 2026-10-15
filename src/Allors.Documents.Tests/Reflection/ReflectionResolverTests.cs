// <copyright file="ReflectionResolverTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Reflection.Tests
{
    using System.Collections.Generic;
    using Allors.Document.Reflection;
    using Xunit;

    public class ReflectionResolverTests
    {
        private static readonly ReflectionResolver Resolver = ReflectionResolver.Instance;

        [Theory]
        [InlineData("FirstName", "John")]
        [InlineData("firstName", "John")]
        [InlineData("lower", "a lower-case property")]
        [InlineData("FullName", "John Doe")]
        [InlineData("fullName", "John Doe")]
        [InlineData("field", "a field")]
        [InlineData("underscored", "an underscored field")]
        [InlineData("_underscored", "an underscored field")]
        public void MembersResolveAsWrittenOrWithTheFirstLetterUpperCased(string name, string expected)
        {
            Assert.True(Resolver.TryGetMember(new Person(), name, out var value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData("FIRSTNAME")]
        [InlineData("fIRSTNAME")]
        [InlineData("Lower")]
        [InlineData("Field")]
        [InlineData("Missing")]
        [InlineData("Nothing")]
        [InlineData("With")]
        [InlineData("")]
        public void OtherSpellingsAndUnknownNamesAreNotMembers(string name)
        {
            Assert.False(Resolver.TryGetMember(new Person(), name, out var value));
            Assert.Null(value);
        }

        [Theory]
        [InlineData("Type")]
        [InlineData("HashCode")]
        public void TheMethodsEveryObjectHasAreNotMembers(string name) => Assert.False(Resolver.TryGetMember(new Person(), name, out _));

        [Fact]
        public void AMemberThatIsNullIsFoundWithANullValue()
        {
            Assert.True(Resolver.TryGetMember(new Person(), "Address", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void AStringIndexerAnswersEveryName()
        {
            var indexed = new Indexed();

            Assert.True(Resolver.TryGetMember(indexed, "Known", out var known));
            Assert.Equal("known", known);
            Assert.True(Resolver.TryGetMember(indexed, "whatever", out var whatever));
            Assert.Equal("indexed whatever", whatever);
        }

        [Fact]
        public void ADictionaryHasItsKeysAndNotItsProperties()
        {
            var dictionary = new Dictionary<string, object> { ["Name"] = "John" };

            Assert.True(Resolver.TryGetMember(dictionary, "Name", out var name));
            Assert.Equal("John", name);
            Assert.False(Resolver.TryGetMember(dictionary, "Count", out _));
            Assert.False(Resolver.TryGetMember(dictionary, "Keys", out _));
        }

        [Fact]
        public void ValuesAreReadEachTime()
        {
            var person = new Person();

            Resolver.TryGetMember(person, "FirstName", out var before);
            person.FirstName = "Jane";
            Resolver.TryGetMember(person, "FirstName", out var after);

            Assert.Equal("John", before);
            Assert.Equal("Jane", after);
        }

        [Theory]
        [InlineData("Age")]
        [InlineData("age")]
        public void ANumberResolvesAsItself(string name)
        {
            Assert.True(Resolver.TryGetMember(new Person(), name, out var value));
            Assert.Equal(42, value);
        }
    }
}
