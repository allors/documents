// <copyright file="ResolverContractTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Documents;
    using Allors.Documents.Reflection;
    using Xunit;

    /// <summary>The parts of the contract every provider shares: iteration, truthiness, formatting, ordering, pictures and dictionary keys.</summary>
    public class ResolverContractTests
    {
        public static IEnumerable<object[]> Resolvers => new[]
        {
            new object[] { DictionaryResolver.Instance },
            new object[] { ReflectionResolver.Instance },
        };

        private enum Colour
        {
            Red,
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void NullYieldsNoItems(IResolver resolver) => Assert.Empty(resolver.Items(null));

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void TextAndScalarsYieldThemselvesOnce(IResolver resolver)
        {
            Assert.Equal(new object[] { "text" }, resolver.Items("text"));
            Assert.Equal(new object[] { 42 }, resolver.Items(42));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void ACollectionYieldsItsItemsWithoutNulls(IResolver resolver)
        {
            Assert.Equal(new object[] { "a", "b" }, resolver.Items(new[] { "a", null, "b" }));
            Assert.Equal(new object[] { 1, 2 }, resolver.Items(new List<object> { 1, null, 2 }));
            Assert.Empty(resolver.Items(new object[0]));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void ADictionaryYieldsItsKeys(IResolver resolver)
        {
            var dictionary = new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 };

            Assert.Equal(new object[] { "b", "a" }, resolver.Items(dictionary));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void NullFalseBlankZeroAndEmptyAreFalse(IResolver resolver)
        {
            Assert.False(resolver.IsTrue(null));
            Assert.False(resolver.IsTrue(false));
            Assert.False(resolver.IsTrue(string.Empty));
            Assert.False(resolver.IsTrue("   "));
            Assert.False(resolver.IsTrue(0));
            Assert.False(resolver.IsTrue(0m));
            Assert.False(resolver.IsTrue(0.0));
            Assert.False(resolver.IsTrue(0L));
            Assert.False(resolver.IsTrue(new object[0]));
            Assert.False(resolver.IsTrue(new List<int>()));
            Assert.False(resolver.IsTrue(Enumerable.Empty<int>()));
            Assert.False(resolver.IsTrue(new Dictionary<string, object>()));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void EverythingElseIsTrue(IResolver resolver)
        {
            Assert.True(resolver.IsTrue(true));
            Assert.True(resolver.IsTrue("x"));
            Assert.True(resolver.IsTrue(1));
            Assert.True(resolver.IsTrue(-0.5m));
            Assert.True(resolver.IsTrue(new object[] { null }));
            Assert.True(resolver.IsTrue(new DateTime(2026, 10, 14)));
            Assert.True(resolver.IsTrue(new object()));
            Assert.True(resolver.IsTrue(Colour.Red));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void ValuesPrintPlainlyByDefault(IResolver resolver)
        {
            Assert.Equal(string.Empty, resolver.Format(null, Formatting.Default));
            Assert.Equal("as typed  ", resolver.Format("as typed  ", Formatting.Default));
            Assert.Equal("true", resolver.Format(true, Formatting.Default));
            Assert.Equal("false", resolver.Format(false, Formatting.Default));
            Assert.Equal("2.5", resolver.Format(2.5m, Formatting.Default));
            Assert.Equal("1000000", resolver.Format(1000000, Formatting.Default));
            Assert.Equal("Red", resolver.Format(Colour.Red, Formatting.Default));
            Assert.Equal("10/14/2026 14:30:00", resolver.Format(new DateTime(2026, 10, 14, 14, 30, 0), Formatting.Default));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void ACollectionPrintsItsItemsRunTogether(IResolver resolver)
        {
            // What StringTemplate printed for a collection bound as a value: every item, formatted, nothing between them, nulls skipped
            Assert.Equal("firstsecond", resolver.Format(new[] { "first", null, "second" }, Formatting.Default));
            Assert.Equal("12.5", resolver.Format(new object[] { 1, 2.5m }, Formatting.Default));
            Assert.Equal("ab", resolver.Format(new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 }, Formatting.Default));
            Assert.Equal(string.Empty, resolver.Format(Array.Empty<string>(), Formatting.Default));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void ANullFormattingMeansTheDefault(IResolver resolver) => Assert.Equal("2.5", resolver.Format(2.5m, null));

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void NullSortsFirst(IResolver resolver)
        {
            Assert.Equal(0, resolver.Compare(null, null, Formatting.Default));
            Assert.True(resolver.Compare(null, "a", Formatting.Default) < 0);
            Assert.True(resolver.Compare("a", null, Formatting.Default) > 0);
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void TextSortsInTheCulture(IResolver resolver)
        {
            Assert.True(resolver.Compare("apple", "Banana", Formatting.Default) < 0);
            Assert.True(resolver.Compare("Banana", "apple", Formatting.Default) > 0);
            Assert.Equal(0, resolver.Compare("same", "same", Formatting.Default));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void NumbersSortAcrossTypes(IResolver resolver)
        {
            Assert.True(resolver.Compare(2, 10m, Formatting.Default) < 0);
            Assert.True(resolver.Compare(2.5, 2, Formatting.Default) > 0);
            Assert.Equal(0, resolver.Compare(3L, 3, Formatting.Default));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void ComparableValuesOfOneTypeSort(IResolver resolver)
        {
            Assert.True(resolver.Compare(new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), Formatting.Default) < 0);
            Assert.True(resolver.Compare(true, false, Formatting.Default) > 0);
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void ValuesThatCannotBeOrderedThrow(IResolver resolver)
        {
            Assert.Throws<ArgumentException>(() => resolver.Compare(1, "1", Formatting.Default));
            Assert.Throws<ArgumentException>(() => resolver.Compare(new object(), new object(), Formatting.Default));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void BytesAreAPictureAndNothingElseIs(IResolver resolver)
        {
            var bytes = new byte[] { 1, 2, 3 };

            Assert.Same(bytes, resolver.Picture(bytes));
            Assert.Null(resolver.Picture("Pictures/logo.png"));
            Assert.Null(resolver.Picture(null));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void DictionaryKeysAreMembersAsWritten(IResolver resolver)
        {
            var dictionary = new Dictionary<string, object> { ["Name"] = "John", ["Empty"] = null };

            Assert.True(resolver.TryGetMember(dictionary, "Name", out var name));
            Assert.Equal("John", name);
            Assert.True(resolver.TryGetMember(dictionary, "Empty", out var empty));
            Assert.Null(empty);
            Assert.False(resolver.TryGetMember(dictionary, "name", out _));
            Assert.False(resolver.TryGetMember(dictionary, "Missing", out _));
            Assert.False(resolver.TryGetMember(dictionary, "Count", out _));
        }

        [Theory]
        [MemberData(nameof(Resolvers))]
        public void OtherDictionaryTypesWorkToo(IResolver resolver)
        {
            var hashtable = new Hashtable { ["Name"] = "John" };
            IReadOnlyDictionary<string, object> readOnly = new ReadOnlyOnly { ["Name"] = "Jane" };

            Assert.True(resolver.TryGetMember(hashtable, "Name", out var fromHashtable));
            Assert.Equal("John", fromHashtable);
            Assert.True(resolver.TryGetMember(readOnly, "Name", out var fromReadOnly));
            Assert.Equal("Jane", fromReadOnly);
        }

        /// <summary>A dictionary that is read-only only, to exercise the IReadOnlyDictionary path.</summary>
        private sealed class ReadOnlyOnly : IReadOnlyDictionary<string, object>
        {
            private readonly Dictionary<string, object> inner = new Dictionary<string, object>();

            public object this[string key]
            {
                get => this.inner[key];
                set => this.inner[key] = value;
            }

            public IEnumerable<string> Keys => this.inner.Keys;

            public IEnumerable<object> Values => this.inner.Values;

            public int Count => this.inner.Count;

            public bool ContainsKey(string key) => this.inner.ContainsKey(key);

            public bool TryGetValue(string key, out object value) => this.inner.TryGetValue(key, out value);

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => this.inner.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}
