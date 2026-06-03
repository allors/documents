// <copyright file="XmlText.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Templating;

using System.Text;

/// <summary>
/// Sanitizes bound values for XML text content: null or whitespace-only values become empty,
/// and control characters that are invalid in XML 1.0 are stripped (tab, line feed and
/// carriage return are kept). Markup escaping is left to the XML writer.
/// </summary>
internal static class XmlText
{
    internal static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var hasInvalid = false;
        foreach (var chr in text)
        {
            if (IsInvalid(chr))
            {
                hasInvalid = true;
                break;
            }
        }

        if (!hasInvalid)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var chr in text)
        {
            if (!IsInvalid(chr))
            {
                builder.Append(chr);
            }
        }

        return builder.ToString();
    }

    private static bool IsInvalid(char chr) => chr < 32 && chr != '\t' && chr != '\n' && chr != '\r';
}
