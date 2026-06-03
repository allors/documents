// <copyright file="ExpressionTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.Expressions;

using System.Collections.Generic;
using Allors.Documents;
using Allors.Documents.Expressions;
using Xunit;

public class ExpressionTests
{
    [Fact]
    public void ResolvesPathFromDictionaryModel()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Name"] = "Koen" });

        Assert.Equal("Koen", Evaluate("Name", scope));
    }

    [Fact]
    public void ResolvesNestedPathThroughReflection()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Person"] = new Person { FirstName = "Jane" } });

        Assert.Equal("Jane", Evaluate("Person.FirstName", scope));
    }

    [Fact]
    public void MissingRootResolvesToNull()
    {
        var scope = Scope(new Dictionary<string, object?>());

        Assert.Null(Evaluate("Missing", scope));
    }

    [Fact]
    public void NullPropagatesThroughPath()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Person"] = null });

        Assert.Null(Evaluate("Person.FirstName.Length", scope));
    }

    [Fact]
    public void MissingMemberResolvesToNull()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Person"] = new Person() });

        Assert.Null(Evaluate("Person.Missing", scope));
    }

    [Fact]
    public void LocalsShadowModelValues()
    {
        var scope = Scope(new Dictionary<string, object?> { ["p"] = "outer" });
        var child = scope.CreateChild();
        child.Set("p", "inner");

        Assert.Equal("inner", Evaluate("p", child));
        Assert.Equal("outer", Evaluate("p", scope));
    }

    [Fact]
    public void ChildScopeStillSeesModel()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Name"] = "Koen" });
        var child = scope.CreateChild();
        child.Set("p", 1);

        Assert.Equal("Koen", Evaluate("Name", child));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData("", false)]
    [InlineData("a", true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(0.0, false)]
    [InlineData(0.5, true)]
    public void TruthinessOfScalars(object? value, bool expected) => Assert.Equal(expected, Truthiness.IsTruthy(value));

    [Fact]
    public void TruthinessOfCollections()
    {
        Assert.False(Truthiness.IsTruthy(new string[0]));
        Assert.True(Truthiness.IsTruthy(new[] { "a" }));
        Assert.False(Truthiness.IsTruthy(new List<int>()));
        Assert.True(Truthiness.IsTruthy(new List<int> { 1 }));
    }

    [Fact]
    public void TruthinessOfObjects() => Assert.True(Truthiness.IsTruthy(new Person()));

    [Theory]
    [InlineData("Age == 42", true)]
    [InlineData("Age != 42", false)]
    [InlineData("Age == 41", false)]
    [InlineData("Age < 43", true)]
    [InlineData("Age > 43", false)]
    [InlineData("Age <= 42", true)]
    [InlineData("Age >= 43", false)]
    public void NumericComparisons(string expression, bool expected)
    {
        var scope = Scope(new Dictionary<string, object?> { ["Age"] = 42 });

        Assert.Equal(expected, Evaluate(expression, scope));
    }

    [Fact]
    public void NumericComparisonAcrossNumericTypes()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Long"] = 3L, ["Double"] = 3.0 });

        Assert.Equal(true, Evaluate("Long == Double", scope));
        Assert.Equal(true, Evaluate("Long == 3", scope));
        Assert.Equal(true, Evaluate("Double < 3.5", scope));
    }

    [Theory]
    [InlineData("Name == 'Koen'", true)]
    [InlineData("Name == \"Koen\"", true)]
    [InlineData("Name != 'Jane'", true)]
    [InlineData("Name < 'L'", true)]
    public void StringComparisons(string expression, bool expected)
    {
        var scope = Scope(new Dictionary<string, object?> { ["Name"] = "Koen" });

        Assert.Equal(expected, Evaluate(expression, scope));
    }

    [Fact]
    public void NullEquality()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Person"] = null, ["Name"] = "Koen" });

        Assert.Equal(true, Evaluate("Person == null", scope));
        Assert.Equal(false, Evaluate("Name == null", scope));
        Assert.Equal(true, Evaluate("Name != null", scope));
    }

    [Fact]
    public void RelationalWithNullIsFalse()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Person"] = null });

        Assert.Equal(false, Evaluate("Person < 3", scope));
        Assert.Equal(false, Evaluate("Person >= 3", scope));
    }

    [Theory]
    [InlineData("true && true", true)]
    [InlineData("true && false", false)]
    [InlineData("false || true", true)]
    [InlineData("false || false", false)]
    [InlineData("!false", true)]
    [InlineData("!Name", false)]
    [InlineData("Name && Missing", false)]
    [InlineData("(Name || Missing) && true", true)]
    public void LogicalOperators(string expression, bool expected)
    {
        var scope = Scope(new Dictionary<string, object?> { ["Name"] = "Koen" });

        Assert.Equal(expected, Evaluate(expression, scope));
    }

    [Fact]
    public void AndShortCircuits()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Person"] = null });

        // Would be null-propagated anyway, but the right side must not affect the result.
        Assert.Equal(false, Evaluate("Person && Person.FirstName", scope));
    }

    [Fact]
    public void TruthinessOfBarePath()
    {
        var scope = Scope(new Dictionary<string, object?> { ["Name"] = "Koen", ["Empty"] = "" });

        Assert.Equal("Koen", Evaluate("Name", scope));
        Assert.True(Truthiness.IsTruthy(Evaluate("Name", scope)));
        Assert.False(Truthiness.IsTruthy(Evaluate("Empty", scope)));
        Assert.False(Truthiness.IsTruthy(Evaluate("Missing", scope)));
    }

    [Theory]
    [InlineData("a >")]
    [InlineData("a b")]
    [InlineData("(a")]
    [InlineData("a ?")]
    [InlineData("a.")]
    [InlineData("a = b")]
    [InlineData("a & b")]
    [InlineData("'unterminated")]
    [InlineData("")]
    public void InvalidExpressionsThrow(string expression) =>
        Assert.Throws<TemplateException>(() => Evaluate(expression, Scope(new Dictionary<string, object?>())));

    [Fact]
    public void ParseErrorsHaveReadableMessages()
    {
        var exception = Assert.Throws<TemplateException>(() => ExpressionParser.Parse("Person.FirstName >"));

        var error = Assert.Single(exception.Errors);
        Assert.Contains("Person.FirstName >", error.Message);
        Assert.Equal(exception.Message, error.ToString());
    }

    [Fact]
    public void PathParserRejectsNonPaths()
    {
        Assert.Throws<TemplateException>(() => ExpressionParser.ParsePath("a == b"));
        Assert.Throws<TemplateException>(() => ExpressionParser.ParsePath("42"));
        Assert.Throws<TemplateException>(() => ExpressionParser.ParsePath("a..b"));
    }

    [Fact]
    public void LiteralValues()
    {
        var scope = Scope(new Dictionary<string, object?>());

        Assert.Equal(42m, Evaluate("42", scope));
        Assert.Equal(3.14m, Evaluate("3.14", scope));
        Assert.Equal("text", Evaluate("'text'", scope));
        Assert.Equal(true, Evaluate("true", scope));
        Assert.Equal(false, Evaluate("false", scope));
        Assert.Null(Evaluate("null", scope));
    }

    [Fact]
    public void EscapedQuotesInStrings()
    {
        var scope = Scope(new Dictionary<string, object?>());

        Assert.Equal("it's", Evaluate(@"'it\'s'", scope));
        Assert.Equal("a\"b", Evaluate("\"a\\\"b\"", scope));
    }

    private static object? Evaluate(string expression, RenderScope scope) => ExpressionParser.Parse(expression).Evaluate(scope);

    private static RenderScope Scope(IReadOnlyDictionary<string, object?> model) => new(model, ValueAccessor.Default);

    private sealed class Person
    {
        public string? FirstName { get; set; }
    }
}
