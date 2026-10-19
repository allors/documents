// <copyright file="ValueShape.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A leaf of a model: a value of one kind, without members, items or variants. The members of a closed <see cref="DictionaryShape"/>
    /// and the units of a host's own shapes are leaves; <see cref="Text"/>, <see cref="Number"/> and the others are the shared ones.
    /// </summary>
    public sealed class ValueShape : IShape
    {
        public static readonly ValueShape Text = new ValueShape(ValueKind.Text);

        public static readonly ValueShape Number = new ValueShape(ValueKind.Number);

        public static readonly ValueShape Boolean = new ValueShape(ValueKind.Boolean);

        public static readonly ValueShape Date = new ValueShape(ValueKind.Date);

        public static readonly ValueShape Time = new ValueShape(ValueKind.Time);

        public static readonly ValueShape DateTime = new ValueShape(ValueKind.DateTime);

        public static readonly ValueShape Picture = new ValueShape(ValueKind.Picture);

        public static readonly ValueShape Other = new ValueShape(ValueKind.Other);

        private readonly string? name;

        /// <summary>A leaf of this kind, named for findings; the kind's own name when there is none. Open, Object and Collection are no leaves.</summary>
        public ValueShape(ValueKind kind, string? name = null)
        {
            if (kind == ValueKind.Open || kind == ValueKind.Object || kind == ValueKind.Collection)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "a leaf is a text, number, boolean, date, time, datetime, picture or other value");
            }

            this.Kind = kind;
            this.name = name;
        }

        public string Name => this.name ?? this.Kind.ToString();

        public ValueKind Kind { get; }

        public IEnumerable<string> Members => Enumerable.Empty<string>();

        public IShape? Element => null;

        public IEnumerable<IShape> Variants => Enumerable.Empty<IShape>();

        /// <summary>The leaf of this kind, shared.</summary>
        public static ValueShape Of(ValueKind kind)
        {
            switch (kind)
            {
                case ValueKind.Text:
                    return Text;
                case ValueKind.Number:
                    return Number;
                case ValueKind.Boolean:
                    return Boolean;
                case ValueKind.Date:
                    return Date;
                case ValueKind.Time:
                    return Time;
                case ValueKind.DateTime:
                    return DateTime;
                case ValueKind.Picture:
                    return Picture;
                case ValueKind.Other:
                    return Other;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "a leaf is a text, number, boolean, date, time, datetime, picture or other value");
            }
        }

        public IShape? Member(string name) => null;

        public override string ToString() => this.Name;
    }
}
