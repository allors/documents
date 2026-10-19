// <copyright file="ReflectionShapeTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
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

        [Fact]
        public void CollectionsHaveAnElementAndScalarsDoNot()
        {
            var person = ReflectionShape.Of<Person>();

            Assert.True(person.Member("Tags").IsCollection);
            Assert.Equal("String", person.Member("Tags").Element.Name);
            Assert.True(person.Member("Friends").IsCollection);
            Assert.Same(person, person.Member("Friends").Element);
            Assert.True(person.Member("Things").IsCollection);
            Assert.Same(DictionaryShape.Open, person.Member("Things").Element);

            Assert.False(person.IsCollection);
            Assert.Null(person.Element);
            Assert.False(person.Member("FirstName").IsCollection);
            Assert.Null(person.Member("FirstName").Element);
            Assert.False(person.Member("Age").IsCollection);
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
