// <copyright file="ValueKind.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    /// <summary>
    /// What kind of value a shape stands for: what a loop does with it, which pattern family formats it, whether it sorts and whether it is a picture.
    /// Validation reports a tag that asks a value for what its kind cannot give; the render prints what it can either way.
    /// </summary>
    public enum ValueKind
    {
        /// <summary>Anything: an open dictionary or an untyped value. Every member exists, a loop iterates it and no format is wrong.</summary>
        Open,

        /// <summary>A value with members and no items: a class, a closed dictionary. It cannot sort or be a picture, and prints through ToString.</summary>
        Object,

        /// <summary>A collection: a loop visits its items, whose shape is <see cref="IShape.Element"/>. It cannot sort or be a picture.</summary>
        Collection,

        /// <summary>Text, or a name such as an enum's: prints as it is, sorts in the culture.</summary>
        Text,

        /// <summary>A number: prints with the number pattern in the number culture.</summary>
        Number,

        /// <summary>A boolean: prints its true or false word.</summary>
        Boolean,

        /// <summary>A date without a time of day: prints with the date pattern in the date culture.</summary>
        Date,

        /// <summary>A time of day: prints with the time pattern in the date culture.</summary>
        Time,

        /// <summary>A date with a time of day: prints with the datetime pattern, or the date pattern at midnight, in the date culture.</summary>
        DateTime,

        /// <summary>The bytes of a picture: what a picture tag binds.</summary>
        Picture,

        /// <summary>A value of another kind, such as a time span or an identifier: it prints with the tag's format, and validation leaves it be.</summary>
        Other,
    }
}
