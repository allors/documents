// <copyright file="Parts.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// What an OpenDocument text is made of, as its package holds it: content.xml and styles.xml, the parts that render; the manifest, which
    /// lists the files; and every other file by its name, among them the mimetype, meta.xml with the document settings, settings.xml and the
    /// pictures. A package (.odt) is read as it is; a flat document (.fodt), one XML file with the pictures embedded, is read into the same parts.
    /// </summary>
    internal sealed class Parts
    {
        public Parts(byte[] content, byte[] styles, byte[] manifest, Dictionary<string, byte[]> files)
        {
            this.Content = content;
            this.Styles = styles;
            this.Manifest = manifest;
            this.Files = files;
        }

        /// <summary>content.xml: the body with the automatic styles it uses.</summary>
        public byte[] Content { get; }

        /// <summary>styles.xml: the styles and the master pages with their headers and footers; null when the document has none.</summary>
        public byte[] Styles { get; }

        /// <summary>META-INF/manifest.xml; null when the document has none.</summary>
        public byte[] Manifest { get; }

        /// <summary>Every other file by its name in the package.</summary>
        public Dictionary<string, byte[]> Files { get; }

        /// <summary>
        /// Reads a package or a flat document, whichever the bytes are. Throws <see cref="InvalidDataException"/> when they are neither,
        /// when the package has no content.xml or when the flat document is not an office:document.
        /// </summary>
        public static Parts Read(byte[] document)
        {
            var parts = IsPackage(document) ? Package.Read(document) : Flat.Read(document);
            if (parts.Content == null)
            {
                throw new InvalidDataException("the package has no content.xml: it is not an OpenDocument text");
            }

            return parts;
        }

        /// <summary>A package is a zip file, which starts with the signature of its first entry; anything else is read as XML.</summary>
        private static bool IsPackage(byte[] document) => document.Length >= 2 && document[0] == 0x50 && document[1] == 0x4B;
    }
}
