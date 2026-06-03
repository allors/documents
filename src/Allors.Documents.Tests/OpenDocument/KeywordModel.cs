// <copyright file="KeywordModel.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

using Allors.Documents;

/// <summary>Regression model: keyword identifiers must be escaped in the generated accessors.</summary>
[DocumentModel]
public class KeywordModel
{
    public string? @class { get; set; }

    public int @event { get; set; }

    // A keyword-named type, reached transitively from this model.
    public @struct? Nested { get; set; }
}

public class @struct
{
    public string? @string { get; set; }
}
