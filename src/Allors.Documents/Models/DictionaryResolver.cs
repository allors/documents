// <copyright file="DictionaryResolver.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>Resolves members as dictionary keys, exactly as written, and nothing else. No reflection.</summary>
    public class DictionaryResolver : Resolver
    {
        public static readonly DictionaryResolver Instance = new DictionaryResolver();

        public override bool TryGetMember(object scope, string name, out object value) => TryGetDictionaryMember(scope, name, out value);
    }
}
