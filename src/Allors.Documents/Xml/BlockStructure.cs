// <copyright file="BlockStructure.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Xml;

    /// <summary>
    /// Where a block may begin, branch and end. The engine takes the lowest common ancestor of the begin and the end and removes the
    /// children of that ancestor that hold the block's tags, keeping or repeating what sits between: tags in one paragraph cost only their
    /// text runs, tags in rows of one table cost the rows. Tags in two cells of one row, in two tables, or one in a table and one outside
    /// would remove cells or whole tables. An else sits in a child of that ancestor of its own, between the two that hold the begin and the end.
    /// </summary>
    internal static class BlockStructure
    {
        private const string TextNamespace = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private const string TableNamespace = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";

        /// <summary>What is wrong with where this block begins, branches and ends, in the author's terms; null when it is fine.</summary>
        public static string? Problem(XmlPair pair)
        {
            var begin = pair.BeginSibling;
            var finish = pair.EndSibling;
            if (begin == null || finish == null)
            {
                return null;
            }

            if (Is(begin, TableNamespace, "table-cell") || Is(finish, TableNamespace, "table-cell"))
            {
                return "the tags sit in two cells of one row, which removes those cells and breaks the row";
            }

            if (Is(begin, TableNamespace, "table") && Is(finish, TableNamespace, "table"))
            {
                return "the tags sit in two tables, which removes both tables";
            }

            if (Is(begin, TableNamespace, "table"))
            {
                return "the block begins in a table row and ends outside the table, which would repeat or remove the whole table";
            }

            if (Is(finish, TableNamespace, "table"))
            {
                return "the block begins outside a table and ends in a table row, which would remove the whole table";
            }

            foreach (var sibling in pair.SeparatorSiblings)
            {
                if (sibling == null)
                {
                    return "the else sits outside the block";
                }

                if (sibling == begin)
                {
                    return "the else sits in the " + Describe(begin) + " that holds the block's begin tag, which is removed with that tag: give the else a " + Describe(begin) + " of its own";
                }

                if (sibling == finish)
                {
                    return "the else sits in the " + Describe(finish) + " that holds the block's <@end>, which is removed with that tag: give the else a " + Describe(finish) + " of its own";
                }

                if (Is(sibling, TableNamespace, "table"))
                {
                    return "the else sits in a table while the block's tags sit outside it, which would remove the table";
                }
            }

            return null;
        }

        /// <summary>The message of a finding for a problem: the rule, then what breaks it.</summary>
        public static string Message(string problem) => "a block begins, branches and ends in paragraphs of one container or in rows of one table; " + problem;

        /// <summary>True when the block sits inside one paragraph, heading, text run or link: its tags cost only themselves and text may go between its items.</summary>
        public static bool IsInline(XmlPair pair)
        {
            var container = pair.Container;
            return container != null && container.NamespaceURI == TextNamespace && (container.LocalName == "p" || container.LocalName == "h" || container.LocalName == "span" || container.LocalName == "a");
        }

        /// <summary>What the author calls the element a block tag sits in: a paragraph, a row, a list, a text run.</summary>
        public static string Describe(XmlElement container) =>
            Is(container, TextNamespace, "p") || Is(container, TextNamespace, "h") ? "paragraph"
            : Is(container, TableNamespace, "table-row") ? "row"
            : Is(container, TextNamespace, "list") ? "list"
            : Is(container, TextNamespace, "span") ? "text run"
            : container.LocalName;

        private static bool Is(XmlElement element, string ns, string localName) => element.NamespaceURI == ns && element.LocalName == localName;
    }
}
