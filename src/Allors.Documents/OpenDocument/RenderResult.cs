// <copyright file="RenderResult.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>What a render produced: the document, and what the template asked of the model that it could not give.</summary>
    public sealed class RenderResult
    {
        internal RenderResult(byte[] document, IReadOnlyList<Finding> findings)
        {
            this.Document = document;
            this.Findings = findings;
        }

        /// <summary>The rendered document: an OpenDocument package, or a flat document when the settings of the render asked for one.</summary>
        public byte[] Document { get; }

        /// <summary>
        /// Every member the template asked for that the model does not have, every loop that could not be ordered and every tag that could not
        /// be read, each once, in document order; empty when the template and the model agree. The document is complete either way:
        /// a missing value prints blank, an unordered loop keeps the collection's order and an unreadable tag prints as typed.
        /// </summary>
        public IReadOnlyList<Finding> Findings { get; }

        /// <summary>The findings that say a value is missing from the document: an unknown member, a tag that could not be read, a picture that could not be given.</summary>
        public IEnumerable<Finding> Errors => this.Findings.Where(v => v.Severity == FindingSeverity.Error);

        /// <summary>The findings that say the document is complete but not as the template asked: a loop that keeps its order, a picture that fills its frame, a map without a row.</summary>
        public IEnumerable<Finding> Warnings => this.Findings.Where(v => v.Severity == FindingSeverity.Warning);
    }
}
