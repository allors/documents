// <copyright file="Package.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;

    /// <summary>
    /// Writes an OpenDocument package the way both engines do: the mimetype entry first, stored and without an extra field, so that the
    /// media type sits at a fixed offset (ODF 1.2 part 3, 3.3); then the rendered parts and the manifest, deflated; then every other file
    /// of the template, pictures that are compressed already stored and the rest deflated.
    /// </summary>
    internal static class Package
    {
        public static byte[] Save(byte[] mimetype, byte[] content, byte[] styles, byte[] manifest, IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            using (var zip = new MemoryStream())
            {
                using (var archive = new ZipArchive(zip, ZipArchiveMode.Create))
                {
                    if (mimetype != null)
                    {
                        Add(archive, OpenDocumentTemplate.MimetypeFileName, mimetype, CompressionLevel.NoCompression);
                    }

                    Add(archive, OpenDocumentTemplate.ContentFileName, content, CompressionLevel.Optimal);

                    if (styles != null)
                    {
                        Add(archive, OpenDocumentTemplate.StylesFileName, styles, CompressionLevel.Optimal);
                    }

                    if (manifest != null)
                    {
                        Add(archive, OpenDocumentTemplate.MetaFileName, manifest, CompressionLevel.Optimal);
                    }

                    foreach (var file in files)
                    {
                        if (file.Key.Equals(OpenDocumentTemplate.MimetypeFileName))
                        {
                            continue;
                        }

                        Add(archive, file.Key, file.Value, CompressedImage.Is(file.Value) ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                    }
                }

                return zip.ToArray();
            }
        }

        private static void Add(ZipArchive archive, string name, byte[] bytes, CompressionLevel level)
        {
            var entry = archive.CreateEntry(name, level);
            using (var stream = entry.Open())
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
