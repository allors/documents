// <copyright file="ReflectionShape.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Reflection
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Document.Models;

    /// <summary>
    /// The shape of a type, read through reflection, with the member rules of <see cref="ReflectionResolver"/>.
    /// Text, numbers, dates and the like are leaves. A dictionary or an object-typed value is open. An enumerable is a collection of its element type.
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

        public IEnumerable<string> Members => Reflection.Members.Names(this.type);

        public bool IsCollection => ElementType(this.type) != null;

        public IShape Element
        {
            get
            {
                var elementType = ElementType(this.type);
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
