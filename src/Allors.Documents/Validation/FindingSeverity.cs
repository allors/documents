// <copyright file="FindingSeverity.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>How bad a finding is: an error renders wrong or not at all, a warning renders but probably not as meant.</summary>
    public enum FindingSeverity
    {
        Error,

        Warning,
    }
}
