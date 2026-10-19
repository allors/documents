// <copyright file="Length.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A length as OpenDocument and SVG write one: a number followed by a unit, <c>2.44in</c> or <c>10mm</c>. The units are the absolute ones
    /// both know, mm, cm, in, pt, pc and px, a px being a 96th of an inch. A relative unit, such as % or em, is no length here.
    /// </summary>
    internal static class Length
    {
        private static readonly Regex LengthRegex = new Regex(@"^\s*([+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)\s*([A-Za-z%]*)\s*$");

        /// <summary>
        /// Reads a length into its number and its unit, in lower case. A number without a unit takes the default unit, when there is one.
        /// False for text that is no length, or a unit that is not absolute.
        /// </summary>
        public static bool TryParse(string text, string defaultUnit, out double value, out string unit)
        {
            value = 0;
            unit = null;

            var match = text == null ? null : LengthRegex.Match(text);
            if (match == null || !match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            unit = match.Groups[2].Value.Length == 0 ? defaultUnit : match.Groups[2].Value.ToLowerInvariant();
            return unit != null && TryMillimetresPer(unit, out _);
        }

        /// <summary>The value in another unit.</summary>
        public static double Convert(double value, string fromUnit, string toUnit)
        {
            TryMillimetresPer(fromUnit, out var from);
            TryMillimetresPer(toUnit, out var to);
            return value * from / to;
        }

        /// <summary>The length as OpenDocument writes one: the number with up to four decimals, then the unit.</summary>
        public static string Format(double value, string unit) => value.ToString("0.####", CultureInfo.InvariantCulture) + unit;

        private static bool TryMillimetresPer(string unit, out double millimetres)
        {
            switch (unit)
            {
                case "mm":
                    millimetres = 1;
                    return true;
                case "cm":
                    millimetres = 10;
                    return true;
                case "in":
                    millimetres = 25.4;
                    return true;
                case "pt":
                    millimetres = 25.4 / 72;
                    return true;
                case "pc":
                    millimetres = 25.4 / 6;
                    return true;
                case "px":
                    millimetres = 25.4 / 96;
                    return true;
                default:
                    millimetres = 0;
                    return false;
            }
        }
    }
}
