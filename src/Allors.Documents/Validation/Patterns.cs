// <copyright file="Patterns.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>
    /// The grammar of the patterns a template may carry, checked without a value: a format .NET would print, but not as the author meant,
    /// is as wrong as one it rejects. A date pattern is a standard letter or a custom pattern of specifier runs; a number pattern is a
    /// standard letter with its precision or a custom pattern with a digit placeholder.
    /// </summary>
    internal static class Patterns
    {
        /// <summary>The standard date and time format letters.</summary>
        public const string StandardDateLetters = "dDfFgGmMoOrRstTuUyY";

        /// <summary>The letters a custom date and time pattern may use outside quotes: day, month, year, hour, minute, second, fraction, meridiem, offset, kind and era.</summary>
        public const string CustomDateLetters = "dMyhHmsfFtzKg";

        /// <summary>The standard numeric format letters.</summary>
        public const string StandardNumberLetters = "BbCcDdEeFfGgNnPpRrXx";

        /// <summary>
        /// True for a standard date and time format letter, or a custom pattern whose unquoted ASCII letters are all specifiers, with quoted text
        /// closed and every escape followed by a character. A pattern of one character is standard or wrong.
        /// </summary>
        public static bool IsDate(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            if (pattern.Length == 1)
            {
                return StandardDateLetters.IndexOf(pattern[0]) >= 0;
            }

            for (var i = 0; i < pattern.Length; i++)
            {
                var chr = pattern[i];
                if (chr == '\'' || chr == '"')
                {
                    var close = pattern.IndexOf(chr, i + 1);
                    if (close < 0)
                    {
                        return false;
                    }

                    i = close;
                }
                else if (chr == '\\')
                {
                    if (i + 1 == pattern.Length)
                    {
                        return false;
                    }

                    i++;
                }
                else if (IsAsciiLetter(chr) && CustomDateLetters.IndexOf(chr) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True for a standard numeric format letter, with digits of precision or without, or a custom pattern with at least one 0 or # outside
        /// quotes, with quoted text closed and every escape followed by a character.
        /// </summary>
        public static bool IsNumber(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            if (StandardNumberLetters.IndexOf(pattern[0]) >= 0 && IsDigits(pattern, 1))
            {
                return true;
            }

            var placeholder = false;
            for (var i = 0; i < pattern.Length; i++)
            {
                var chr = pattern[i];
                if (chr == '\'' || chr == '"')
                {
                    var close = pattern.IndexOf(chr, i + 1);
                    if (close < 0)
                    {
                        return false;
                    }

                    i = close;
                }
                else if (chr == '\\')
                {
                    if (i + 1 == pattern.Length)
                    {
                        return false;
                    }

                    i++;
                }
                else if (chr == '0' || chr == '#')
                {
                    placeholder = true;
                }
            }

            return placeholder;
        }

        private static bool IsAsciiLetter(char chr) => (chr >= 'a' && chr <= 'z') || (chr >= 'A' && chr <= 'Z');

        private static bool IsDigits(string text, int from)
        {
            for (var i = from; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
