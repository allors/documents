// <copyright file="FindingKind.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Validation
{
    /// <summary>What a finding is about.</summary>
    public enum FindingKind
    {
        /// <summary>A tag, or the description of a picture, that cannot be read as a tag.</summary>
        MalformedTag,

        /// <summary>A block without an end, or an end without a block.</summary>
        UnbalancedBlock,

        /// <summary>A path segment the model does not have, on a binding, a loop, a condition, a sort key or a picture.</summary>
        UnknownMember,

        /// <summary>A loop over a value that is not a collection; it runs once.</summary>
        NotACollection,

        /// <summary>An attribute a tag of that kind does not know.</summary>
        UnknownAttribute,

        /// <summary>A culture name that is no culture.</summary>
        BadCulture,

        /// <summary>A date, time or number pattern .NET rejects.</summary>
        BadPattern,

        /// <summary>A value an attribute does not take, such as empty="maybe" on a picture.</summary>
        BadValue,

        /// <summary>A loop variable the engine reserves.</summary>
        ReservedName,

        /// <summary>A block whose begin and end are not in paragraphs of one container or in rows of one table.</summary>
        BlockStructure,

        /// <summary>Text or tags in the paragraph or row that holds a block tag; they are removed with it.</summary>
        MixedText,

        /// <summary>A document setting the library does not know.</summary>
        UnknownSetting,

        /// <summary>A loop whose sort keys cannot be ordered against each other at render time; it keeps the collection's order.</summary>
        CannotSort,

        /// <summary>A value bound to a picture frame that is not a picture, or bytes in a format the library does not know; the frame leaves the document.</summary>
        NotAPicture,
    }
}
