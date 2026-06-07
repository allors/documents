// <copyright file="TextPage.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

using Allors.Documents;

/// <summary>
/// A non-generic subclass of a closed generic — the documented, reflection-free escape hatch for
/// generic models. The generator emits an accessor for the inherited members.
/// </summary>
[DocumentModel]
public sealed class TextPage : PagedResult<string>
{
}
