// <copyright file="ReflectionShape.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Reflection
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Documents;

    /// <summary>
    /// The shape of a type, read through reflection, with the member rules of <see cref="ReflectionResolver"/>. The kind follows the type:
    /// text and enums, numbers, booleans, dates and times, a byte array as a picture, any other enumerable as a collection of its element type,
    /// any other class as an object; a dictionary or an object-typed value is open. Every type keeps its members, as the resolver reads them.
    /// </summary>
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the members of a type through reflection.")]
#endif
    public sealed class ReflectionShape : IShape
    {
        private static readonly ConcurrentDictionary<Type, IShape> ShapeByType = new ConcurrentDictionary<Type, IShape>();

        private readonly Type type;

        private ReflectionShape(Type type) => this.type = type;

        public string Name => this.type.Name;

        public Type Type => this.type;

        public ValueKind Kind => KindOf(this.type);

        public IEnumerable<string> Members => Reflection.Members.Names(this.type);

        public IShape Element
        {
            get
            {
                var elementType = this.Kind == ValueKind.Collection ? ElementType(this.type) : null;
                return elementType == null ? null : Of(elementType);
            }
        }

        /// <summary>A class has no variants here: reflection does not enumerate subclasses.</summary>
        public IEnumerable<IShape> Variants => Enumerable.Empty<IShape>();

        /// <summary>The shape of this type.</summary>
        public static IShape Of<T>() => Of(typeof(T));

        /// <summary>
        /// The root a template rendered with <c>OpenDocumentTemplate&lt;T&gt;</c> sees: the public properties of T by their exact names,
        /// each with the shape of its type. Nested members follow the first-letter rule; the root does not.
        /// </summary>
        public static IShape Arguments<T>() => Arguments(typeof(T));

        /// <summary>The root a template rendered with this model type sees; see <see cref="Arguments{T}"/>.</summary>
        public static IShape Arguments(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            var members = new Dictionary<string, IShape>(StringComparer.Ordinal);
            foreach (var property in type.GetProperties())
            {
                if (!members.ContainsKey(property.Name))
                {
                    members[property.Name] = Of(property.PropertyType);
                }
            }

            return new DictionaryShape(members, type.Name);
        }

        /// <summary>The shape of this type: open for object and dictionaries, otherwise a reflection shape.</summary>
        public static IShape Of(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return ShapeByType.GetOrAdd(type, Create);
        }

        public IShape Member(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            var member = Reflection.Members.Find(this.type, name);
            return member == null ? null : Of(member.Type);
        }

        public override string ToString() => this.Name;

        private static IShape Create(Type type)
        {
            if (type == typeof(object) || typeof(IDictionary).IsAssignableFrom(type) || IsGenericDictionary(type))
            {
                return DictionaryShape.Open;
            }

            return new ReflectionShape(type);
        }

        private static bool IsGenericDictionary(Type type) =>
            type.IsInterface && type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

        /// <summary>The kind of a type: a nullable has the kind of its value type, an enum is text, a byte array is a picture, any other enumerable a collection.</summary>
        private static ValueKind KindOf(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(string) || type.IsEnum)
            {
                return ValueKind.Text;
            }

            if (type == typeof(bool))
            {
                return ValueKind.Boolean;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return ValueKind.DateTime;
            }

#if NET
            if (type == typeof(DateOnly))
            {
                return ValueKind.Date;
            }

            if (type == typeof(TimeOnly))
            {
                return ValueKind.Time;
            }
#endif

            if (type == typeof(byte[]))
            {
                return ValueKind.Picture;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return ValueKind.Number;
            }

            if (ElementType(type) != null)
            {
                return ValueKind.Collection;
            }

            return typeof(IFormattable).IsAssignableFrom(type) || type.IsPrimitive ? ValueKind.Other : ValueKind.Object;
        }

        /// <summary>The element type of a collection, or null for text and anything that is not enumerable.</summary>
        private static Type ElementType(Type type)
        {
            if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
            {
                return null;
            }

            if (type.IsArray)
            {
                return type.GetElementType();
            }

            var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? type
                : type.GetInterfaces().FirstOrDefault(v => v.IsGenericType && v.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            return enumerable?.GetGenericArguments()[0] ?? typeof(object);
        }
    }
}
