// <copyright file="Resolver.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// The parts of a resolver that do not depend on how members are read: iteration, truthiness, formatting, ordering and pictures.
    /// Derive from it and implement <see cref="TryGetMember"/>; override the rest for your own value types.
    /// </summary>
    public abstract class Resolver : IResolver
    {
        public abstract bool TryGetMember(object scope, string name, out object value);

        /// <summary>
        /// Null yields nothing. Text yields itself once. A dictionary yields its keys. Any other enumerable yields its items, null items skipped.
        /// Anything else yields itself once.
        /// </summary>
        public virtual IEnumerable<object> Items(object value)
        {
            switch (value)
            {
                case null:
                    return Array.Empty<object>();
                case string _:
                    return new[] { value };
                case IDictionary dictionary:
                    return Enumerate(dictionary.Keys);
                case IEnumerable enumerable:
                    return Enumerate(enumerable);
                default:
                    return new[] { value };
            }
        }

        /// <summary>Null, false, text that is empty or white space, zero and an empty collection are false; everything else is true.</summary>
        public virtual bool IsTrue(object value)
        {
            switch (value)
            {
                case null:
                    return false;
                case bool flag:
                    return flag;
                case string text:
                    return !string.IsNullOrWhiteSpace(text);
                case ICollection collection:
                    return collection.Count > 0;
                case IEnumerable enumerable:
                    return enumerable.GetEnumerator().MoveNext();
                default:
                    return !IsZero(value);
            }
        }

        /// <summary>
        /// Null prints blank and text prints as is. A boolean prints the true or false word. A date prints with the date pattern at midnight
        /// and the datetime pattern otherwise, in the date culture; a number prints with the number pattern in the number culture; the tag's
        /// format wins over the patterns. Enums print their name. A collection prints its items one after the other, each formatted, with
        /// nothing between them: a loop is what separates them. Everything else prints through <see cref="IFormattable"/> with the culture, or ToString.
        /// </summary>
        public virtual string Format(object value, Formatting formatting)
        {
            formatting = formatting ?? Formatting.Default;

            switch (value)
            {
                case null:
                    return string.Empty;
                case string text:
                    return text;
                case bool flag:
                    return flag ? formatting.True : formatting.False;
                case Enum _:
                    return value.ToString();
                case IEnumerable _:
                    var items = new StringBuilder();
                    foreach (var item in this.Items(value))
                    {
                        items.Append(this.Format(item, formatting));
                    }

                    return items.ToString();
                case DateTime dateTime:
                    var dateTimeKind = dateTime.TimeOfDay == TimeSpan.Zero ? ValueKind.Date : ValueKind.DateTime;
                    return dateTime.ToString(formatting.PatternFor(dateTimeKind), formatting.CultureFor(dateTimeKind));
                case DateTimeOffset dateTimeOffset:
                    var dateTimeOffsetKind = dateTimeOffset.TimeOfDay == TimeSpan.Zero ? ValueKind.Date : ValueKind.DateTime;
                    return dateTimeOffset.ToString(formatting.PatternFor(dateTimeOffsetKind), formatting.CultureFor(dateTimeOffsetKind));
#if NET
                case DateOnly date:
                    return date.ToString(formatting.PatternFor(ValueKind.Date), formatting.CultureFor(ValueKind.Date));
                case TimeOnly time:
                    return time.ToString(formatting.PatternFor(ValueKind.Time), formatting.CultureFor(ValueKind.Time));
#endif
                case IFormattable formattable:
                    return IsNumber(value)
                        ? formattable.ToString(formatting.PatternFor(ValueKind.Number), formatting.CultureFor(ValueKind.Number))
                        : formattable.ToString(NotNamed(formatting.Format), formatting.Culture);
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// Null sorts first. Text is compared in the formatting's culture, never in a culture per kind. Numbers compare across numeric types. Other values of one type
        /// compare through <see cref="IComparable"/>. Anything else throws.
        /// </summary>
        public virtual int Compare(object x, object y, Formatting formatting)
        {
            if (x == null)
            {
                return y == null ? 0 : -1;
            }

            if (y == null)
            {
                return 1;
            }

            if (x is string a && y is string b)
            {
                return (formatting ?? Formatting.Default).Culture.CompareInfo.Compare(a, b, CompareOptions.None);
            }

            if (IsNumber(x) && IsNumber(y))
            {
                return Convert.ToDecimal(x, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(y, CultureInfo.InvariantCulture));
            }

            if (x.GetType() == y.GetType() && x is IComparable comparable)
            {
                return comparable.CompareTo(y);
            }

            throw new ArgumentException("cannot order a " + x.GetType().Name + " against a " + y.GetType().Name);
        }

        /// <summary>A byte array is a picture; anything else is not.</summary>
        public virtual byte[] Picture(object value) => value as byte[];

        /// <summary>True when the scope is a dictionary, whose members are its keys and nothing else.</summary>
        protected static bool IsDictionary(object scope) => scope is IDictionary || scope is IDictionary<string, object> || scope is IReadOnlyDictionary<string, object>;

        /// <summary>Reads a key from a dictionary scope, exactly as written. False when the scope is not a dictionary or has no such key.</summary>
        protected static bool TryGetDictionaryMember(object scope, string name, out object value)
        {
            switch (scope)
            {
                case IDictionary<string, object> generic:
                    return generic.TryGetValue(name, out value);
                case IReadOnlyDictionary<string, object> readOnly:
                    return readOnly.TryGetValue(name, out value);
                case IDictionary dictionary:
                    if (dictionary.Contains(name))
                    {
                        value = dictionary[name];
                        return true;
                    }

                    value = null;
                    return false;
                default:
                    value = null;
                    return false;
            }
        }

        /// <summary>True for the built-in numeric types.</summary>
        protected static bool IsNumber(object value)
        {
            switch (value)
            {
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case float _:
                case double _:
                case decimal _:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsZero(object value)
        {
            switch (value)
            {
                case byte number:
                    return number == 0;
                case sbyte number:
                    return number == 0;
                case short number:
                    return number == 0;
                case ushort number:
                    return number == 0;
                case int number:
                    return number == 0;
                case uint number:
                    return number == 0;
                case long number:
                    return number == 0;
                case ulong number:
                    return number == 0;
                case float number:
                    return number == 0;
                case double number:
                    return number == 0;
                case decimal number:
                    return number == 0;
                default:
                    return false;
            }
        }

        private static string NotNamed(string format) => Formatting.IsNamedFormat(format) ? null : format;

        private static IEnumerable<object> Enumerate(IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }
}
