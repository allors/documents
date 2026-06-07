// <copyright file="Expression.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Expressions;

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>A parsed template expression that can be evaluated against a <see cref="RenderScope"/>.</summary>
internal abstract class Expression
{
    internal abstract object? Evaluate(RenderScope scope);
}

/// <summary>A constant value: a number, a quoted string, true, false or null.</summary>
internal sealed class LiteralExpression : Expression
{
    internal LiteralExpression(object? value) => this.Value = value;

    internal object? Value { get; }

    internal override object? Evaluate(RenderScope scope) => this.Value;

    public override string ToString() => this.Value?.ToString() ?? "null";
}

/// <summary>A member path such as <c>Person.FirstName</c>. Null propagates: any missing or null segment yields null.</summary>
internal sealed class PathExpression : Expression
{
    internal PathExpression(IReadOnlyList<string> segments) => this.Segments = segments;

    internal IReadOnlyList<string> Segments { get; }

    internal string Root => this.Segments[0];

    internal override object? Evaluate(RenderScope scope)
    {
        if (!scope.TryResolve(this.Segments[0], out var current))
        {
            return null;
        }

        for (var i = 1; i < this.Segments.Count; i++)
        {
            if (!scope.Accessor.TryGetMember(current, this.Segments[i], out current))
            {
                return null;
            }
        }

        return current;
    }

    public override string ToString() => string.Join(".", this.Segments);
}

/// <summary>Logical negation of the truthiness of its operand.</summary>
internal sealed class NotExpression : Expression
{
    internal NotExpression(Expression operand) => this.Operand = operand;

    internal Expression Operand { get; }

    internal override object? Evaluate(RenderScope scope) => !Truthiness.IsTruthy(this.Operand.Evaluate(scope));

    public override string ToString() => "!" + this.Operand;
}

internal enum BinaryOperator
{
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
    And,
    Or,
}

/// <summary>
/// A binary operation. Logical operators short-circuit on truthiness.
/// Equality is null-safe; numbers compare numerically across numeric types — exactly (as
/// <see cref="decimal"/>) unless a binary floating point operand forces a <see cref="double"/> comparison.
/// Relational comparisons are numeric, ordinal for strings, and <see cref="IComparable"/> for
/// same-typed operands; incomparable operands (including null) compare as false.
/// </summary>
internal sealed class BinaryExpression : Expression
{
    internal BinaryExpression(BinaryOperator @operator, Expression left, Expression right)
    {
        this.Operator = @operator;
        this.Left = left;
        this.Right = right;
    }

    internal BinaryOperator Operator { get; }

    internal Expression Left { get; }

    internal Expression Right { get; }

    internal override object? Evaluate(RenderScope scope)
    {
        switch (this.Operator)
        {
            case BinaryOperator.And:
                return Truthiness.IsTruthy(this.Left.Evaluate(scope)) && Truthiness.IsTruthy(this.Right.Evaluate(scope));

            case BinaryOperator.Or:
                return Truthiness.IsTruthy(this.Left.Evaluate(scope)) || Truthiness.IsTruthy(this.Right.Evaluate(scope));
        }

        var left = this.Left.Evaluate(scope);
        var right = this.Right.Evaluate(scope);

        return this.Operator switch
        {
            BinaryOperator.Equal => AreEqual(left, right),
            BinaryOperator.NotEqual => !AreEqual(left, right),
            BinaryOperator.LessThan => Compare(left, right) is < 0,
            BinaryOperator.GreaterThan => Compare(left, right) is > 0,
            BinaryOperator.LessThanOrEqual => Compare(left, right) is <= 0,
            BinaryOperator.GreaterThanOrEqual => Compare(left, right) is >= 0,
            _ => throw new InvalidOperationException($"Unknown operator: {this.Operator}"),
        };
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return IsBinaryFloatingPoint(left) || IsBinaryFloatingPoint(right)
                ? ToDouble(left) == ToDouble(right)
                : ToDecimal(left) == ToDecimal(right);
        }

        return left.Equals(right);
    }

    private static int? Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return null;
        }

        if (IsNumeric(left) && IsNumeric(right))
        {
            return IsBinaryFloatingPoint(left) || IsBinaryFloatingPoint(right)
                ? ToDouble(left).CompareTo(ToDouble(right))
                : ToDecimal(left).CompareTo(ToDecimal(right));
        }

        if (left is string leftString && right is string rightString)
        {
            return string.CompareOrdinal(leftString, rightString);
        }

        if (left.GetType() == right.GetType() && left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        return null;
    }

    private static bool IsNumeric(object value) =>
        value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static bool IsBinaryFloatingPoint(object value) => value is float or double;

    private static double ToDouble(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);

    private static decimal ToDecimal(object value) => Convert.ToDecimal(value, CultureInfo.InvariantCulture);
}

/// <summary>
/// Null/blank coalescing: returns the left operand unless it is "blank" (<see langword="null"/>
/// or a whitespace-only string), in which case the right operand is evaluated and returned.
/// "Blank" mirrors how bindings render empty, so it is broader than C#'s null-only <c>??</c>.
/// </summary>
internal sealed class CoalesceExpression : Expression
{
    internal CoalesceExpression(Expression left, Expression right)
    {
        this.Left = left;
        this.Right = right;
    }

    internal Expression Left { get; }

    internal Expression Right { get; }

    internal override object? Evaluate(RenderScope scope)
    {
        var left = this.Left.Evaluate(scope);
        return IsBlank(left) ? this.Right.Evaluate(scope) : left;

        static bool IsBlank(object? value) => value is null || (value is string text && string.IsNullOrWhiteSpace(text));
    }

    public override string ToString() => $"{this.Left} ?? {this.Right}";
}
