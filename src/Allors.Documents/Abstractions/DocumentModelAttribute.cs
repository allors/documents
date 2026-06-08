// <copyright file="DocumentModelAttribute.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents;

using System;

/// <summary>
/// Marks a type as a document model. The Allors.Documents source generator emits a reflection-free
/// member accessor for the type (and the model types it references) and registers it at module
/// initialization, so template expressions resolve without reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class DocumentModelAttribute : Attribute
{
}
