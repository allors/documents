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
                var manifest = new XmlDocument();
                manifest.Load(new MemoryStream(this.manifestFile));

                contentGenerated = this.ProcessImages(contentGenerated, manifest);
                stylesGenerated = this.ProcessImages(stylesGenerated, manifest);

                using (var output = new MemoryStream())
                {
                    manifest.Save(output);
                    this.manifestFile = output.ToArray();
                }
            }

            foreach (var image in this.images?.Where(v => v.OriginalFullPath != null))
            {
                this.fileByFileName.Add(image.FullPath, image.Contents);
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

                        Save(archive, fileName, kvp.Value, CompressionLevel.Optimal);
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

        private string ProcessImages(string generated, XmlDocument manifest)
        {
            var document = new XmlDocument();
            document.LoadXml(generated);

            var frameElements = document.GetElementsByTagName("draw:frame");
            foreach (XmlElement frameElement in frameElements)
            {
                var drawNs = frameElement.GetNamespaceOfPrefix("draw");
                var name = frameElement.GetAttribute("name", drawNs).Trim();
                var image = this.images.FirstOrDefault(v => v.Name.Equals(name));
                if (image != null)
                {
                    var imageElement = (XmlElement)frameElement.GetElementsByTagName("draw:image")[0];
                    var xlinkNs = imageElement.GetNamespaceOfPrefix("xlink");
                    image.OriginalFullPath = imageElement.GetAttribute("href", xlinkNs);
                    imageElement.SetAttribute("href", xlinkNs, image.FullPath);
                }
            }

            generated = document.OuterXml;

            var documentElement = manifest.DocumentElement;
            var elements = documentElement.ChildNodes.OfType<XmlElement>().ToArray();

            foreach (var element in elements)
            {
                var manifestNs = element.GetNamespaceOfPrefix("manifest");
                var fullPath = element.GetAttribute("full-path", manifestNs);

                foreach (var image in this.images.Where(v => v.OriginalFullPath != null && v.OriginalFullPath.Equals(fullPath)))
                {
                    var newElement = (XmlElement)element.Clone();
                    documentElement.AppendChild(newElement);

                    manifestNs = newElement.GetNamespaceOfPrefix("manifest");
                    newElement.SetAttribute("full-path", manifestNs, image.FullPath);
                }
            }

            return generated;
        }
    }
}
