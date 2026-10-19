// <copyright file="Samples.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

#pragma warning disable SA1401 // Fields must be private: the contract covers public fields.
#pragma warning disable IDE1006 // Naming: the contract covers lower-case names.

    /// <summary>A model with every kind of member the reflection contract covers.</summary>
    public class Person
    {
        public string field = "a field";

        public string _underscored = "an underscored field";

        public string FirstName { get; set; } = "John";

        public string lower { get; set; } = "a lower-case property";

        public int Age { get; set; } = 42;

        public DateTime Birthday { get; set; } = new DateTime(1984, 2, 29);

        public Address Address { get; set; }

        public string[] Tags { get; set; } = { "x", "y" };

        public List<Person> Friends { get; set; } = new List<Person>();

        public IEnumerable Things { get; set; } = new ArrayList();

        public Dictionary<string, object> Extra { get; set; } = new Dictionary<string, object>();

        public object Anything { get; set; } = "anything";

        public string GetFullName() => this.FirstName + " Doe";

        public void GetNothing()
        {
        }

        public string GetWith(int argument) => argument.ToString();
    }

    public class Address
    {
        public string City { get; set; } = "Ghent";
    }

    /// <summary>A model with a string indexer: every name resolves.</summary>
    public class Indexed
    {
        public string Known { get; set; } = "known";

        public string this[string key] => "indexed " + key;
    }
}
