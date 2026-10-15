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
    /// Reads and writes an OpenDocument package (.odt), the zip file that holds a document's parts. The package is written the way Writer
    /// writes one: the mimetype entry first, stored and without an extra field, so that the media type sits at a fixed offset (ODF 1.2 part 3, 3.3);
    /// then the rendered parts and the manifest, deflated; then every other file of the template, pictures that are compressed already stored and the rest deflated.
    /// </summary>
    internal static class Package
    {
        /// <summary>The parts of a package: the two that render and the manifest by themselves, every other file by its name.</summary>
        public static Parts Read(byte[] document)
        {
            byte[] content = null;
            byte[] styles = null;
            byte[] manifest = null;
            var files = new Dictionary<string, byte[]>();

            using (var zip = new MemoryStream(document))
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    byte[] file;
                    using (var memoryStream = new MemoryStream())
                    using (var zipStream = entry.Open())
                    {
                        zipStream.CopyTo(memoryStream);
                        file = memoryStream.ToArray();
                    }

                    switch (entry.FullName)
                    {
                        case OpenDocumentTemplate.ContentFileName:
                            content = file;
                            break;

                        case OpenDocumentTemplate.StylesFileName:
                            styles = file;
                            break;

                        case OpenDocumentTemplate.MetaFileName:
                            manifest = file;
                            break;

                        default:
                            files.Add(entry.FullName, file);
                            break;
                    }
                }
            }

            return new Parts(content, styles, manifest, files);
        }

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

                        Add(archive, file.Key, file.Value, PictureFormat.IsCompressedPicture(file.Value) ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
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
