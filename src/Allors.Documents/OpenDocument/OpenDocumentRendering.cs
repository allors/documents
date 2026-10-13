// <copyright file="OpenDocumentRendering.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Xml;

    using Antlr4.StringTemplate;
    using Antlr4.StringTemplate.Misc;

    public class OpenDocumentRendering
    {
        private const string MainTemplateName = "main";

        private const string XLinkNamespace = "http://www.w3.org/1999/xlink";

        private readonly IDictionary<string, object> model;
        private readonly TemplateGroup ContentTemplateGroup;
        private readonly TemplateGroup StylesTemplateGroup;
        private readonly IDictionary<string, byte[]> fileByFileName;

        private readonly Image[] images;

        private byte[] manifestFile;

        public OpenDocumentRendering(IDictionary<string, object> model, byte[] manifestFile, IDictionary<string, byte[]> imageByName, TemplateGroup contentTemplateGroup, TemplateGroup stylesTemplateGroup, Dictionary<string, byte[]> fileByFileName)
        {
            this.model = model;
            this.manifestFile = manifestFile;
            this.ContentTemplateGroup = contentTemplateGroup;
            this.StylesTemplateGroup = stylesTemplateGroup;
            this.fileByFileName = fileByFileName;

            this.images = imageByName?.Select(v => new Image { Name = v.Key, Contents = v.Value }).ToArray() ?? new Image[0];
        }

        public byte[] Execute()
        {
            var contentGenerated = this.Generate(this.ContentTemplateGroup);
            var stylesGenerated = this.Generate(this.StylesTemplateGroup);

            if (this.images.Length > 0)
            {
                var content = LoadPart(contentGenerated);
                var styles = LoadPart(stylesGenerated);

                var manifest = new XmlDocument();
                manifest.Load(new MemoryStream(this.manifestFile));

                // A frame named after a passed image shows that image instead of the template picture it held
                var replaced = new HashSet<string>();
                this.BindImages(content, replaced);
                this.BindImages(styles, replaced);

                foreach (var image in this.images.Where(v => v.OriginalFullPath != null))
                {
                    this.fileByFileName[image.FullPath] = image.Contents;
                    AddManifestEntry(manifest, image.OriginalFullPath, image.FullPath);
                }

                // A template picture that no frame shows any more stays out of the document
                var shown = new HashSet<string>(Hrefs(content).Concat(Hrefs(styles)));
                foreach (var fullPath in replaced.Where(v => !shown.Contains(v)))
                {
                    this.fileByFileName.Remove(fullPath);
                    RemoveManifestEntries(manifest, fullPath);
                }

                contentGenerated = content.OuterXml;
                stylesGenerated = styles.OuterXml;

                using (var output = new MemoryStream())
                {
                    manifest.Save(output);
                    this.manifestFile = output.ToArray();
                }
            }

            using (var zip = new MemoryStream())
            {
                using (var archive = new ZipArchive(zip, ZipArchiveMode.Create))
                {
                    // ODF 1.2 part 3, 3.3: the mimetype entry comes first, uncompressed and without an extra field, so that the media type sits at a fixed offset
                    if (this.fileByFileName.TryGetValue(OpenDocumentTemplate.MimetypeFileName, out var mimetype))
                    {
                        Save(archive, OpenDocumentTemplate.MimetypeFileName, mimetype, CompressionLevel.NoCompression);
                    }

                    Save(archive, OpenDocumentTemplate.ContentFileName, Encoding.UTF8.GetBytes(contentGenerated), CompressionLevel.Optimal);
                    Save(archive, OpenDocumentTemplate.StylesFileName, Encoding.UTF8.GetBytes(stylesGenerated), CompressionLevel.Optimal);
                    Save(archive, OpenDocumentTemplate.MetaFileName, this.manifestFile, CompressionLevel.Optimal);

                    foreach (var kvp in this.fileByFileName)
                    {
                        var fileName = kvp.Key;
                        if (fileName.Equals(OpenDocumentTemplate.MimetypeFileName))
                        {
                            continue;
                        }

                        Save(archive, fileName, kvp.Value, CompressedImage.Is(kvp.Value) ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                    }
                }

                return zip.ToArray();
            }
        }

        private static void Save(ZipArchive archive, string fileName, byte[] file, CompressionLevel compressionLevel)
        {
            var entry = archive.CreateEntry(fileName, compressionLevel);
            using (var zipStream = entry.Open())
            {
                zipStream.Write(file, 0, file.Length);
            }
        }

        /// <summary>Loads a rendered part. Every white space character in it is the author's, so none is dropped.</summary>
        private static XmlDocument LoadPart(string xml)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.LoadXml(xml);
            return document;
        }

        private static IEnumerable<string> Hrefs(XmlDocument document)
        {
            foreach (XmlElement element in document.GetElementsByTagName("*"))
            {
                foreach (XmlAttribute attribute in element.Attributes)
                {
                    if (attribute.LocalName.Equals("href") && attribute.NamespaceURI.Equals(XLinkNamespace))
                    {
                        yield return attribute.Value;
                    }
                }
            }
        }

        /// <summary>The new picture takes over the manifest entry of the picture it replaces, media type included; a picture bound in several parts gets one entry.</summary>
        private static void AddManifestEntry(XmlDocument manifest, string originalFullPath, string fullPath)
        {
            var entries = ManifestEntries(manifest).ToArray();
            if (entries.Any(v => FullPath(v).Equals(fullPath)))
            {
                return;
            }

            var original = entries.FirstOrDefault(v => FullPath(v).Equals(originalFullPath));
            if (original != null)
            {
                var entry = (XmlElement)original.Clone();
                entry.SetAttribute("full-path", entry.GetNamespaceOfPrefix("manifest"), fullPath);
                manifest.DocumentElement.AppendChild(entry);
            }
        }

        private static void RemoveManifestEntries(XmlDocument manifest, string fullPath)
        {
            foreach (var entry in ManifestEntries(manifest).Where(v => FullPath(v).Equals(fullPath)).ToArray())
            {
                manifest.DocumentElement.RemoveChild(entry);
            }
        }

        private static IEnumerable<XmlElement> ManifestEntries(XmlDocument manifest) => manifest.DocumentElement.ChildNodes.OfType<XmlElement>();

        private static string FullPath(XmlElement entry) => entry.GetAttribute("full-path", entry.GetNamespaceOfPrefix("manifest"));

        private string Generate(TemplateGroup templateGroup)
        {
            var errorBuffer = new ErrorBuffer();

            var contentTemplate = templateGroup.GetInstanceOf(MainTemplateName);

            foreach (var kvp in this.model)
            {
                var argName = kvp.Key;
                var argValue = kvp.Value;

                if (contentTemplate.impl.HasFormalArgs && !contentTemplate.impl.FormalArguments.Any(v => argName.Equals(v.Name)))
                {
                    continue;
                }

                contentTemplate.Add(argName, argValue);
            }

            var contentGenerated = contentTemplate.Render();

            if (errorBuffer.Errors.Count > 0)
            {
                throw new TemplateException(errorBuffer.Errors);
            }

            return contentGenerated;
        }

        /// <summary>
        /// Points every frame named after a passed image at that image and records the template picture it held.
        /// A frame without a picture, such as a text box, is left alone.
        /// </summary>
        private void BindImages(XmlDocument document, HashSet<string> replaced)
        {
            foreach (XmlElement frameElement in document.GetElementsByTagName("draw:frame"))
            {
                var drawNs = frameElement.GetNamespaceOfPrefix("draw");
                var name = frameElement.GetAttribute("name", drawNs).Trim();
                var image = this.images.FirstOrDefault(v => v.Name.Equals(name));
                if (image == null)
                {
                    continue;
                }

                var imageElements = frameElement.GetElementsByTagName("draw:image");
                if (imageElements.Count == 0)
                {
                    continue;
                }

                var imageElement = (XmlElement)imageElements[0];
                var xlinkNs = imageElement.GetNamespaceOfPrefix("xlink");
                var href = imageElement.GetAttribute("href", xlinkNs);

                replaced.Add(href);
                image.OriginalFullPath = image.OriginalFullPath ?? href;
                imageElement.SetAttribute("href", xlinkNs, image.FullPath);
            }
        }
    }
}
