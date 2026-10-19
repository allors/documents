// <copyright file="Scope.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>
    /// What a path may start with at a point of the render: the loop variables and counters in scope, innermost first, then a member of the root.
    /// Each loop item pushes a scope; the root scope has no name.
    /// </summary>
    internal sealed class Scope
    {
        private readonly Scope parent;
        private readonly string name;
        private readonly object value;
        private readonly bool counter;

        public Scope(object root) => this.Root = root;

        private Scope(Scope parent, string name, object value, bool counter)
        {
            this.parent = parent;
            this.name = name;
            this.value = value;
            this.counter = counter;
            this.Root = parent.Root;
        }

        /// <summary>The model, whose members a path starts with when no loop in scope declares its first name.</summary>
        public object Root { get; }

        /// <summary>A scope with a loop variable, or a loop counter, in front of this one.</summary>
        public Scope Push(string name, object value, bool counter = false) => new Scope(this, name, value, counter);

        /// <summary>The value of the innermost loop variable or counter with this name; false when no loop in scope declares it.</summary>
        public bool TryGetVariable(string name, out object value)
        {
            var scope = this.Find(name);
            value = scope?.value;
            return scope != null;
        }

        /// <summary>True when the innermost loop variable or counter with this name is a counter.</summary>
        public bool IsCounter(string name) => this.Find(name)?.counter == true;

        private Scope Find(string name)
        {
            for (var scope = this; scope != null; scope = scope.parent)
            {
                if (scope.name != null && scope.name == name)
                {
                    return scope;
                }
            }

            return null;
        }
    }
}
