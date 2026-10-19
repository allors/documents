// <copyright file="ReflectionShapeTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Allors.Documents;
    using Allors.Documents.Reflection;
    using Xunit;

    public class ReflectionShapeTests
    {
        [Fact]
        public void AClassListsItsPropertiesGetMethodsAndFields()
        {
            var shape = ReflectionShape.Of<Person>();

            Assert.Equal("Person", shape.Name);
            Assert.Equal(new[] { "FirstName", "lower", "Age", "Birthday", "Address", "Tags", "Friends", "Things", "Extra", "Anything", "FullName", "field", "_underscored" }, shape.Members);
        }

        [Theory]
        [InlineData("FirstName", "String")]
        [InlineData("firstName", "String")]
        [InlineData("Age", "Int32")]
        [InlineData("fullName", "String")]
        [InlineData("underscored", "String")]
        [InlineData("Address", "Address")]
        public void AMemberHasTheShapeOfItsType(string name, string shapeName)
        {
            var member = ReflectionShape.Of<Person>().Member(name);

            Assert.NotNull(member);
            Assert.Equal(shapeName, member.Name);
        }

        [Theory]
        [InlineData("FIRSTNAME")]
        [InlineData("Missing")]
        [InlineData("Nothing")]
        [InlineData("")]
        [InlineData(null)]
        public void UnknownNamesHaveNoShape(string name) => Assert.Null(ReflectionShape.Of<Person>().Member(name));

        [Fact]
        public void PathsWalkThroughMembers()
        {
            Assert.NotNull(ReflectionShape.Of<Person>().Member("Address").Member("City"));
            Assert.Null(ReflectionShape.Of<Person>().Member("Address").Member("Street"));
        }

        [Theory]
        [InlineData(typeof(string), ValueKind.Text)]
        [InlineData(typeof(DayOfWeek), ValueKind.Text)]
        [InlineData(typeof(int), ValueKind.Number)]
        [InlineData(typeof(decimal), ValueKind.Number)]
        [InlineData(typeof(double?), ValueKind.Number)]
        [InlineData(typeof(bool), ValueKind.Boolean)]
        [InlineData(typeof(bool?), ValueKind.Boolean)]
        [InlineData(typeof(DateTime), ValueKind.DateTime)]
        [InlineData(typeof(DateTimeOffset), ValueKind.DateTime)]
        [InlineData(typeof(DateOnly), ValueKind.Date)]
        [InlineData(typeof(TimeOnly), ValueKind.Time)]
        [InlineData(typeof(byte[]), ValueKind.Picture)]
        [InlineData(typeof(string[]), ValueKind.Collection)]
        [InlineData(typeof(List<Person>), ValueKind.Collection)]
        [InlineData(typeof(IEnumerable), ValueKind.Collection)]
        [InlineData(typeof(Person), ValueKind.Object)]
        [InlineData(typeof(Guid), ValueKind.Other)]
        [InlineData(typeof(TimeSpan), ValueKind.Other)]
        [InlineData(typeof(char), ValueKind.Other)]
        [InlineData(typeof(object), ValueKind.Open)]
        [InlineData(typeof(Hashtable), ValueKind.Open)]
        public void TheKindFollowsTheType(Type type, ValueKind kind) => Assert.Equal(kind, ReflectionShape.Of(type).Kind);

        /// <summary>A byte array is a picture, not a collection of bytes, so it has no element; its members stay, as the resolver reads them.</summary>
        [Fact]
        public void APictureHasNoElementAndKeepsItsMembers()
        {
            var shape = ReflectionShape.Of<byte[]>();

            Assert.Equal(ValueKind.Picture, shape.Kind);
            Assert.Null(shape.Element);
            Assert.NotNull(shape.Member("Length"));
            Assert.NotNull(ReflectionShape.Of<string>().Member("Length"));
        }

        [Fact]
        public void CollectionsHaveAnElementAndScalarsDoNot()
        {
            var person = ReflectionShape.Of<Person>();

            Assert.Equal(ValueKind.Collection, person.Member("Tags").Kind);
            Assert.Equal("String", person.Member("Tags").Element.Name);
            Assert.Equal(ValueKind.Collection, person.Member("Friends").Kind);
            Assert.Same(person, person.Member("Friends").Element);
            Assert.Equal(ValueKind.Collection, person.Member("Things").Kind);
            Assert.Same(DictionaryShape.Open, person.Member("Things").Element);

            Assert.Equal(ValueKind.Object, person.Kind);
            Assert.Null(person.Element);
            Assert.Equal(ValueKind.Text, person.Member("FirstName").Kind);
            Assert.Null(person.Member("FirstName").Element);
            Assert.Equal(ValueKind.Number, person.Member("Age").Kind);
        }

        [Fact]
        public void DictionariesAndObjectsAreOpen()
        {
            var person = ReflectionShape.Of<Person>();

            Assert.Same(DictionaryShape.Open, person.Member("Extra"));
            Assert.Same(DictionaryShape.Open, person.Member("Anything"));
            Assert.Same(DictionaryShape.Open, ReflectionShape.Of<Hashtable>());
            Assert.Same(DictionaryShape.Open, ReflectionShape.Of<IDictionary<string, object>>());
            Assert.Same(DictionaryShape.Open, ReflectionShape.Of<object>());
        }

        [Fact]
        public void AStringIndexerMakesEveryNameAMember()
        {
            var shape = ReflectionShape.Of<Indexed>();

            Assert.Equal("String", shape.Member("Known").Name);
            Assert.Equal("String", shape.Member("whatever").Name);
        }

        [Fact]
        public void ShapesAreSharedPerType() => Assert.Same(ReflectionShape.Of<Person>(), ReflectionShape.Of(typeof(Person)));

        /// <summary>Validation and rendering must agree: what the shape lists resolves, and what resolves the shape knows.</summary>
        [Fact]
        public void TheShapeAndTheResolverAgree()
        {
            var shape = ReflectionShape.Of<Person>();
            var person = new Person();

            foreach (var member in shape.Members)
            {
                Assert.True(ReflectionResolver.Instance.TryGetMember(person, member, out _), member);
            }

            foreach (var name in new[] { "FirstName", "firstName", "FIRSTNAME", "lower", "Lower", "fullName", "field", "Field", "underscored", "Missing", "With", "Nothing" })
            {
                Assert.Equal(ReflectionResolver.Instance.TryGetMember(person, name, out _), shape.Member(name) != null);
            }
        }

        /// <summary>The items a loop visits have the element shape, or the element shape is open.</summary>
        [Fact]
        public void TheShapeAndTheResolverAgreeOnCollections()
        {
            var shape = ReflectionShape.Of<Person>();
            var person = new Person { Friends = { new Person() }, Things = new ArrayList { 1, "two" } };

            foreach (var member in new[] { "Tags", "Friends", "Things" })
            {
                var element = shape.Member(member).Element;
                ReflectionResolver.Instance.TryGetMember(person, member, out var value);

                Assert.NotEmpty(ReflectionResolver.Instance.Items(value));
                Assert.All(ReflectionResolver.Instance.Items(value), item => Assert.True(element == DictionaryShape.Open || element == ReflectionShape.Of(item.GetType()), member));
            }
        }
    }
}
