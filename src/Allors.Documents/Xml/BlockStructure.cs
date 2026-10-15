// <copyright file="BlockStructure.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.Xml
{
    using System.Xml;

    /// <summary>
    /// Where a block may begin and end. The engine takes the lowest common ancestor of the two tags and removes the two children of that
    /// ancestor that hold them, keeping or repeating what sits between: tags in one paragraph cost only their text runs, tags in two rows of
    /// one table cost the rows. Tags in two cells of one row, in two tables, or one in a table and one outside would remove cells or whole tables.
    /// </summary>
    internal static class BlockStructure
    {
        private const string TableNamespace = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

        /// <summary>What is wrong with where this block begins and ends, in the author's terms; null when it is fine.</summary>
        public static string Problem(XmlPair pair)
        {
            var begin = pair.BeginSibling;
            var finish = pair.EndSibling;
            if (begin == null || finish == null)
            {
                return null;
            }

            if (Is(begin, "table-cell") || Is(finish, "table-cell"))
            {
                return "the tags sit in two cells of one row, which removes those cells and breaks the row";
            }

            if (Is(begin, "table") && Is(finish, "table"))
            {
                return "the tags sit in two tables, which removes both tables";
            }

            if (Is(begin, "table"))
            {
                return "the block begins in a table row and ends outside the table, which would repeat or remove the whole table";
            }

            if (Is(finish, "table"))
            {
                return "the block begins outside a table and ends in a table row, which would remove the whole table";
            }

            return null;
        }

        /// <summary>The message of a finding for a problem: the rule, then what breaks it.</summary>
        public static string Message(string problem) => "a block begins and ends in paragraphs of one container or in rows of one table; " + problem;

        private static bool Is(XmlElement element, string localName) => element.NamespaceURI == TableNamespace && element.LocalName == localName;
    }
}
