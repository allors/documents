// <copyright file="Model.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests.OpenDocument;

public partial class Model
{
    public ModelPerson? Person { get; set; }

    public ModelPerson[]? People { get; set; }

    public string[]? Images { get; set; }
}
