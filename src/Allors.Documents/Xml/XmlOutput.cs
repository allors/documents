// <copyright file="XmlOutput.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.IO;
    using System.Text;
    using System.Xml;

    /// <summary>
    /// Writes a part of the package the way Writer saves one: the declaration as LibreOffice writes it, UTF-8 without a byte order mark,
    /// no new lines of its own and the output left open to the caller.
    /// </summary>
    internal static class XmlOutput
    {
        public static readonly byte[] Declaration = Encoding.ASCII.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");

        public static readonly XmlWriterSettings Settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            NewLineHandling = NewLineHandling.None,
            CloseOutput = false,
        };

        /// <summary>The declaration followed by what the writer produces.</summary>
        public static byte[] Write(Action<XmlWriter> write)
        {
            using (var output = new MemoryStream())
            {
                output.Write(Declaration, 0, Declaration.Length);
                using (var writer = XmlWriter.Create(output, Settings))
                {
                    write(writer);
                }

                return output.ToArray();
            }
        }
    }
}
