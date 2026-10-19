// <copyright file="ReflectionResolver.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Reflection
{
    using Allors.Documents;

    /// <summary>
    /// Resolves members on classes and dictionaries as the StringTemplate engine of 1.x and 2.x did. A dictionary has its keys as written
    /// and nothing else. Any other object has the name with its first letter upper-cased or as written, as a property, a Get method,
    /// a get_ method, a string indexer or a field.
    /// </summary>
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the members of a type through reflection.")]
#endif
    public class ReflectionResolver : Resolver
    {
        public static readonly ReflectionResolver Instance = new ReflectionResolver();

        public override bool TryGetMember(object scope, string name, out object value)
        {
            if (IsDictionary(scope))
            {
                return TryGetDictionaryMember(scope, name, out value);
            }

            if (scope == null || string.IsNullOrEmpty(name))
            {
                value = null;
                return false;
            }

            var member = Members.Find(scope.GetType(), name);
            if (member == null)
            {
                value = null;
                return false;
            }

            value = member.Read(scope);
            return true;
        }
    }
}
