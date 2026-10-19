// <copyright file="TemplateValidation.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The outcome of validating a template against a model shape: the findings in document order, and the inspection they came from.</summary>
    public sealed class TemplateValidation
    {
        internal TemplateValidation(IReadOnlyList<Finding> findings, TemplateInspection inspection)
        {
            this.Findings = findings;
            this.Inspection = inspection;
        }

        /// <summary>Every finding, errors and warnings, in document order: content.xml, then styles.xml, then the document settings.</summary>
        public IReadOnlyList<Finding> Findings { get; }

        public TemplateInspection Inspection { get; }

        /// <summary>True when there is no error; warnings do not count.</summary>
        public bool IsValid => this.Errors.Any() == false;

        public IEnumerable<Finding> Errors => this.Findings.Where(v => v.Severity == FindingSeverity.Error);

        public IEnumerable<Finding> Warnings => this.Findings.Where(v => v.Severity == FindingSeverity.Warning);
    }
}
