// <copyright file="Members.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Reflection
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Member lookup on a type, as the StringTemplate engine of 1.x and 2.x did it: the name with its first letter upper-cased or as written,
    /// as a property, then a Get method, then a get_ method, then a string indexer called with the name, then a field as written or with an underscore.
    /// </summary>
    internal static class Members
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        private static readonly ConcurrentDictionary<Type, IReadOnlyList<string>> NamesByType = new ConcurrentDictionary<Type, IReadOnlyList<string>>();

        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Member?>> MembersByType = new ConcurrentDictionary<Type, ConcurrentDictionary<string, Member?>>();

        /// <summary>A found member: its type and how to read it.</summary>
        internal sealed class Member
        {
            internal Member(Type type, Func<object, object?> read)
            {
                this.Type = type;
                this.Read = read;
            }

            public Type Type { get; }

            public Func<object, object?> Read { get; }
        }

        /// <summary>The names a type declares: properties, fields and Get methods (without the Get), as written, without duplicates.</summary>
#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the members of a type through reflection.")]
#endif
        public static IReadOnlyList<string> Names(Type type) => NamesByType.GetOrAdd(type, ReadNames);

        /// <summary>The member of a type with this name, or null.</summary>
#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the members of a type through reflection.")]
#endif
        public static Member? Find(Type type, string name)
        {
            var members = MembersByType.GetOrAdd(type, _ => new ConcurrentDictionary<string, Member?>(StringComparer.Ordinal));
            return members.GetOrAdd(name, _ => Lookup(type, name));
        }

#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the members of a type through reflection.")]
#endif
        private static IReadOnlyList<string> ReadNames(Type type)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var property in type.GetProperties(Flags).Where(v => v.GetIndexParameters().Length == 0 && v.GetMethod != null))
            {
                if (seen.Add(property.Name))
                {
                    names.Add(property.Name);
                }
            }

            foreach (var method in type.GetMethods(Flags).Where(IsGetter))
            {
                var name = method.Name.Substring(3);
                if (seen.Add(name))
                {
                    names.Add(name);
                }
            }

            foreach (var field in type.GetFields(Flags))
            {
                if (seen.Add(field.Name))
                {
                    names.Add(field.Name);
                }
            }

            return names;
        }

#if NET
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the members of a type through reflection.")]
#endif
        private static Member? Lookup(Type type, string name)
        {
            var upper = char.ToUpperInvariant(name[0]) + name.Substring(1);
            var candidates = upper == name ? new[] { name } : new[] { upper, name };

            foreach (var candidate in candidates)
            {
                var property = type.GetProperties(Flags).FirstOrDefault(v => v.Name == candidate && v.GetIndexParameters().Length == 0 && v.GetMethod != null);
                if (property != null)
                {
                    return new Member(property.PropertyType, property.GetValue);
                }
            }

            foreach (var prefix in new[] { "Get", "get_" })
            {
                foreach (var candidate in candidates)
                {
                    var method = type.GetMethods(Flags).FirstOrDefault(v => v.Name == prefix + candidate && v.GetParameters().Length == 0 && v.ReturnType != typeof(void) && v.DeclaringType != typeof(object));
                    if (method != null)
                    {
                        return new Member(method.ReturnType, scope => method.Invoke(scope, null));
                    }
                }
            }

            var indexer = type.GetProperties(Flags).FirstOrDefault(v => v.GetMethod != null && v.GetIndexParameters().Length == 1 && v.GetIndexParameters()[0].ParameterType == typeof(string));
            if (indexer != null)
            {
                return new Member(indexer.PropertyType, scope => indexer.GetValue(scope, new object[] { name }));
            }

            foreach (var candidate in new[] { name, "_" + name })
            {
                var field = type.GetFields(Flags).FirstOrDefault(v => v.Name == candidate);
                if (field != null)
                {
                    return new Member(field.FieldType, field.GetValue);
                }
            }

            return null;
        }

        /// <summary>A parameterless Get method with a result, other than GetType and GetHashCode, which every object has and no template means.</summary>
        private static bool IsGetter(MethodInfo method) =>
            method.Name.Length > 3 && method.Name.StartsWith("Get", StringComparison.Ordinal) && method.GetParameters().Length == 0 && method.ReturnType != typeof(void) && !method.IsSpecialName && method.DeclaringType != typeof(object);
    }
}
