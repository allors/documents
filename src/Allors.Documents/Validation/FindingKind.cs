// <copyright file="FindingKind.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Validation
{
    /// <summary>What a finding is about.</summary>
    public enum FindingKind
    {
        /// <summary>A tag, the description of a picture, or a map table that cannot be read as the author meant it.</summary>
        MalformedTag,

        /// <summary>A block without an end, an end without a block, or an else that belongs to no condition.</summary>
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

        /// <summary>A block whose begin, elses and end are not in paragraphs of one container or in rows of one table.</summary>
        BlockStructure,

        /// <summary>Text or tags in the paragraph or row that holds a block tag; they are removed with it.</summary>
        MixedText,

        /// <summary>A document setting the library does not know.</summary>
        UnknownSetting,

        /// <summary>A loop whose sort keys cannot be ordered against each other at render time; it keeps the collection's order.</summary>
        CannotSort,

        /// <summary>A value bound to a picture frame that is not a picture, or bytes in a format the library does not know; the frame leaves the document.</summary>
        NotAPicture,

        /// <summary>A picture that fills its frame instead of fitting it as its tag asks, because its bytes do not tell its size or the frame has no size.</summary>
        CannotFit,

        /// <summary>A binding's map attribute that names no map of the template; the value prints as it is.</summary>
        UnknownMap,

        /// <summary>A value a map has no row for, in a map without a default row; the value prints as it is.</summary>
        MissingKey,

        /// <summary>A map no binding prints through.</summary>
        UnusedMap,

        /// <summary>A map without a default row: a value it has no row for prints as it is.</summary>
        MapWithoutDefault,

        /// <summary>A separator on a loop whose tags sit in paragraphs or rows of their own; nothing is written between its items.</summary>
        SeparatorIgnored,
    }
}
