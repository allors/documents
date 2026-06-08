// <copyright file="ExpressionParser.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Expressions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Recursive descent parser for template expressions. Grammar:
/// <code>
/// expression  := coalesceExpr
/// coalesceExpr := orExpr ( "??" orExpr )*
/// orExpr      := andExpr ( "||" andExpr )*
/// andExpr     := compareExpr ( "&amp;&amp;" compareExpr )*
/// compareExpr := unary ( ("==" | "!=" | "&lt;" | "&gt;" | "&lt;=" | "&gt;=") unary )?
/// unary       := "!" unary | primary
/// primary     := literal | path | "(" expression ")"
/// path        := identifier ( "." identifier )*
/// literal     := number | string | "true" | "false" | "null"
/// </code>
/// Parsed expressions are cached by their text.
/// </summary>
internal static class ExpressionParser
{
    private static readonly ConcurrentDictionary<string, Expression> ExpressionCache = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, PathExpression> PathCache = new(StringComparer.Ordinal);

    internal static Expression Parse(string text) =>
        ExpressionCache.GetOrAdd(text.Trim(), static trimmed => new Parser(trimmed).ParseExpression());

    internal static PathExpression ParsePath(string text) =>
        PathCache.GetOrAdd(text.Trim(), static trimmed => new Parser(trimmed).ParsePathOnly());

    private sealed class Parser
    {
        private readonly string text;
        private readonly List<Token> tokens;
        private int index;

        internal Parser(string text)
        {
            this.text = text;
            this.tokens = ExpressionLexer.Tokenize(text);
        }

        private Token Current => this.tokens[this.index];

        internal Expression ParseExpression()
        {
            var expression = this.ParseCoalesce();
            this.Expect(TokenKind.End, "end of expression");
            return expression;
        }

        internal PathExpression ParsePathOnly()
        {
            var path = this.ParsePathSegments();
            this.Expect(TokenKind.End, "end of expression");
            return path;
        }

        private Expression ParseCoalesce()
        {
            var left = this.ParseOr();

            while (this.Current.Kind == TokenKind.Coalesce)
            {
                this.index++;
                left = new CoalesceExpression(left, this.ParseOr());
            }

            return left;
        }

        private Expression ParseOr()
        {
            var left = this.ParseAnd();

            while (this.Current.Kind == TokenKind.Or)
            {
                this.index++;
                left = new BinaryExpression(BinaryOperator.Or, left, this.ParseAnd());
            }

            return left;
        }

        private Expression ParseAnd()
        {
            var left = this.ParseCompare();

            while (this.Current.Kind == TokenKind.And)
            {
                this.index++;
                left = new BinaryExpression(BinaryOperator.And, left, this.ParseCompare());
            }

            return left;
        }

        private Expression ParseCompare()
        {
            var left = this.ParseUnary();

            var @operator = this.Current.Kind switch
            {
                TokenKind.Equal => BinaryOperator.Equal,
                TokenKind.NotEqual => BinaryOperator.NotEqual,
                TokenKind.LessThan => BinaryOperator.LessThan,
                TokenKind.GreaterThan => BinaryOperator.GreaterThan,
                TokenKind.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                TokenKind.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                _ => (BinaryOperator?)null,
            };

            if (@operator is null)
            {
                return left;
            }

            this.index++;
            return new BinaryExpression(@operator.Value, left, this.ParseUnary());
        }

        private Expression ParseUnary()
        {
            if (this.Current.Kind == TokenKind.Not)
            {
                this.index++;
                return new NotExpression(this.ParseUnary());
            }

            return this.ParsePrimary();
        }

        private Expression ParsePrimary()
        {
            var token = this.Current;

            switch (token.Kind)
            {
                case TokenKind.Number:
                    this.index++;
                    if (!decimal.TryParse(token.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    {
                        throw this.Error(token, "number literal is out of range");
                    }

                    return new LiteralExpression(number);

                case TokenKind.String:
                    this.index++;
                    return new LiteralExpression(token.Text);

                case TokenKind.True:
                    this.index++;
                    return new LiteralExpression(true);

                case TokenKind.False:
                    this.index++;
                    return new LiteralExpression(false);

                case TokenKind.Null:
                    this.index++;
                    return new LiteralExpression(null);

                case TokenKind.Identifier:
                    return this.ParsePathSegments();

                case TokenKind.LeftParen:
                    this.index++;
                    var inner = this.ParseCoalesce();
                    this.Expect(TokenKind.RightParen, "')'");
                    return inner;

                default:
                    throw this.Error(token, "expected a value");
            }
        }

        private PathExpression ParsePathSegments()
        {
            var segments = new List<string>();

            var token = this.Current;
            if (token.Kind != TokenKind.Identifier)
            {
                throw this.Error(token, "expected an identifier");
            }

            segments.Add(token.Text);
            this.index++;

            while (this.Current.Kind == TokenKind.Dot)
            {
                this.index++;

                token = this.Current;
                if (token.Kind != TokenKind.Identifier)
                {
                    throw this.Error(token, "expected an identifier after '.'");
                }

                segments.Add(token.Text);
                this.index++;
            }

            return new PathExpression(segments);
        }

        private void Expect(TokenKind kind, string description)
        {
            if (this.Current.Kind != kind)
            {
                throw this.Error(this.Current, $"expected {description}");
            }

            this.index++;
        }

        private TemplateException Error(Token token, string message)
        {
            var found = token.Kind == TokenKind.End ? "end of expression" : $"'{token.Text}'";
            return new TemplateException($"Invalid expression '{this.text}': {message}, found {found} at position {token.Position}.");
        }
    }
}
