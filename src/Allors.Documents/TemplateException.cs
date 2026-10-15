// <copyright file="TemplateException.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Allors.Document.Validation;
    using Antlr4.StringTemplate.Misc;

#pragma warning disable RCS1194 // Implement exception constructors.

    /// <summary>A template that cannot render: its blocks are unbalanced or cut a table, or the StringTemplate engine rejects it.</summary>
    public class TemplateException : Exception
    {
        private static readonly ReadOnlyCollection<TemplateMessage> NoMessages = new ReadOnlyCollection<TemplateMessage>(new TemplateMessage[0]);

        public readonly ReadOnlyCollection<TemplateMessage> TemplateMessages;

        internal TemplateException(ReadOnlyCollection<TemplateMessage> errorBufferErrors)
            : base(string.Join("\n", string.Join("\n", errorBufferErrors)))
        {
            this.TemplateMessages = errorBufferErrors;
            this.Findings = Array.Empty<Finding>();
        }

        internal TemplateException(IReadOnlyList<Finding> findings)
            : base(string.Join("\n", findings))
        {
            this.TemplateMessages = NoMessages;
            this.Findings = findings;
        }

        /// <summary>What is wrong with the template's blocks: a block without an end, an end without a block, or a block that would cut a table. Empty for an error of the StringTemplate engine.</summary>
        public IReadOnlyList<Finding> Findings { get; }
    }
}
