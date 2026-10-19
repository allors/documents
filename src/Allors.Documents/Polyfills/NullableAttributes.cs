// <copyright file="NullableAttributes.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

#if !NETCOREAPP3_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>Says that a parameter is not null when the method returns the given value; netstandard2.0 does not declare the attribute, which the compiler reads by name.</summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) => this.ReturnValue = returnValue;

        /// <summary>The return value under which the parameter is not null.</summary>
        public bool ReturnValue { get; }
    }
}
#endif
