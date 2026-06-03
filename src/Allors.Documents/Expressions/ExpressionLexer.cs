// <copyright file="ExpressionLexer.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Expressions;

using System.Collections.Generic;
using System.Text;

internal enum TokenKind
{
    Identifier,
    Number,
    String,
    True,
    False,
    Null,
    Dot,
    Not,
    Equal,
    NotEqual,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
    And,
    Or,
    LeftParen,
    RightParen,
    End,
}

internal readonly struct Token
{
    internal Token(TokenKind kind, string text, int position)
    {
        this.Kind = kind;
        this.Text = text;
        this.Position = position;
    }

    internal TokenKind Kind { get; }

    internal string Text { get; }

    internal int Position { get; }
}

internal static class ExpressionLexer
{
    internal static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var position = 0;

        while (position < text.Length)
        {
            var chr = text[position];

            if (char.IsWhiteSpace(chr))
            {
                position++;
                continue;
            }

            switch (chr)
            {
                case '.':
                    tokens.Add(new Token(TokenKind.Dot, ".", position));
                    position++;
                    continue;

                case '(':
                    tokens.Add(new Token(TokenKind.LeftParen, "(", position));
                    position++;
                    continue;

                case ')':
                    tokens.Add(new Token(TokenKind.RightParen, ")", position));
                    position++;
                    continue;

                case '!':
                    if (Peek(text, position + 1) == '=')
                    {
                        tokens.Add(new Token(TokenKind.NotEqual, "!=", position));
                        position += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Not, "!", position));
                        position++;
                    }

                    continue;

                case '=':
                    if (Peek(text, position + 1) == '=')
                    {
                        tokens.Add(new Token(TokenKind.Equal, "==", position));
                        position += 2;
                        continue;
                    }

                    throw Error(text, position, "expected '=='");

                case '<':
                    if (Peek(text, position + 1) == '=')
                    {
                        tokens.Add(new Token(TokenKind.LessThanOrEqual, "<=", position));
                        position += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.LessThan, "<", position));
                        position++;
                    }

                    continue;

                case '>':
                    if (Peek(text, position + 1) == '=')
                    {
                        tokens.Add(new Token(TokenKind.GreaterThanOrEqual, ">=", position));
                        position += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.GreaterThan, ">", position));
                        position++;
                    }

                    continue;

                case '&':
                    if (Peek(text, position + 1) == '&')
                    {
                        tokens.Add(new Token(TokenKind.And, "&&", position));
                        position += 2;
                        continue;
                    }

                    throw Error(text, position, "expected '&&'");

                case '|':
                    if (Peek(text, position + 1) == '|')
                    {
                        tokens.Add(new Token(TokenKind.Or, "||", position));
                        position += 2;
                        continue;
                    }

                    throw Error(text, position, "expected '||'");

                case '"':
                case '\'':
                    tokens.Add(LexString(text, ref position));
                    continue;
            }

            if (char.IsDigit(chr))
            {
                tokens.Add(LexNumber(text, ref position));
                continue;
            }

            if (char.IsLetter(chr) || chr == '_')
            {
                tokens.Add(LexIdentifier(text, ref position));
                continue;
            }

            throw Error(text, position, $"unexpected character '{chr}'");
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, text.Length));
        return tokens;
    }

    private static Token LexString(string text, ref int position)
    {
        var start = position;
        var quote = text[position];
        position++;

        var builder = new StringBuilder();
        while (position < text.Length)
        {
            var chr = text[position];

            if (chr == '\\' && position + 1 < text.Length)
            {
                builder.Append(text[position + 1]);
                position += 2;
                continue;
            }

            if (chr == quote)
            {
                position++;
                return new Token(TokenKind.String, builder.ToString(), start);
            }

            builder.Append(chr);
            position++;
        }

        throw Error(text, start, "unterminated string");
    }

    private static Token LexNumber(string text, ref int position)
    {
        var start = position;

        while (position < text.Length && char.IsDigit(text[position]))
        {
            position++;
        }

        if (position + 1 < text.Length && text[position] == '.' && char.IsDigit(text[position + 1]))
        {
            position++;
            while (position < text.Length && char.IsDigit(text[position]))
            {
                position++;
            }
        }

        return new Token(TokenKind.Number, text.Substring(start, position - start), start);
    }

    private static Token LexIdentifier(string text, ref int position)
    {
        var start = position;

        while (position < text.Length && (char.IsLetterOrDigit(text[position]) || text[position] == '_'))
        {
            position++;
        }

        var identifier = text.Substring(start, position - start);

        return identifier switch
        {
            "true" => new Token(TokenKind.True, identifier, start),
            "false" => new Token(TokenKind.False, identifier, start),
            "null" => new Token(TokenKind.Null, identifier, start),
            _ => new Token(TokenKind.Identifier, identifier, start),
        };
    }

    private static char Peek(string text, int position) => position < text.Length ? text[position] : '\0';

    private static TemplateException Error(string text, int position, string message) =>
        new($"Invalid expression '{text}': {message} at position {position}.");
}
