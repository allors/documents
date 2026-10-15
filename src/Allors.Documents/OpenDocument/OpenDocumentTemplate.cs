// <copyright file="OpenDocumentTemplate.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using Allors.Document.Inspection;
    using Allors.Document.Models;
    using Allors.Document.Rendering;
    using Allors.Document.Validation;

    /// <summary>
    /// An OpenDocument text with template tags, read once and rendered any number of times, from any thread. Reading it prepares its
    /// parts: a template whose blocks are unbalanced or would cut a table throws <see cref="TemplateException"/> there, not at a render.
    /// </summary>
    public class OpenDocumentTemplate
    {
        internal const string ContentFileName = "content.xml";

        internal const string StylesFileName = "styles.xml";

        internal const string MetaFileName = "META-INF/manifest.xml";

        internal const string MimetypeFileName = "mimetype";

        private const string DocumentMetaFileName = "meta.xml";

        private readonly Dictionary<string, byte[]> fileByFileName = new Dictionary<string, byte[]>();

        private readonly byte[] manifest;

        private readonly Program contentProgram;

        private readonly Program stylesProgram;

        private readonly IReadOnlyDictionary<string, string> documentSettings;

        private readonly IResolver defaultResolver;

        /// <summary>
        /// Reads a template. Throws <see cref="InvalidDataException"/> when the package is not an OpenDocument text, and
        /// <see cref="TemplateException"/> when a block never ends, an end opens nothing or a block would cut a table.
        /// </summary>
        public OpenDocumentTemplate(byte[] document)
            : this(document, null)
        {
        }

        /// <summary>Reads a template that renders through the given resolver when the settings of a render name none.</summary>
        protected OpenDocumentTemplate(byte[] document, IResolver defaultResolver)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            this.defaultResolver = defaultResolver ?? DictionaryResolver.Instance;

            byte[] contentXml = null;
            byte[] stylesXml = null;

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
                        case ContentFileName:
                            contentXml = file;
                            break;

                        case StylesFileName:
                            stylesXml = file;
                            break;

                        case MetaFileName:
                            this.manifest = file;
                            break;

                        default:
                            this.fileByFileName.Add(entry.FullName, file);
                            break;
                    }
                }
            }

            if (contentXml == null)
            {
                throw new InvalidDataException("the package has no content.xml: it is not an OpenDocument text");
            }

            this.contentProgram = Program.Build(ContentFileName, contentXml);
            this.stylesProgram = stylesXml == null ? null : Program.Build(StylesFileName, stylesXml);
            this.documentSettings = this.fileByFileName.TryGetValue(DocumentMetaFileName, out var meta) ? Inspector.Settings(Inspector.Load(meta)) : new Dictionary<string, string>();
        }

        /// <summary>Reads the tags, bound pictures and document settings of a template without rendering it.</summary>
        public static TemplateInspection Inspect(byte[] document) => Inspector.Inspect(document);

        /// <summary>
        /// Checks a template against the shape of its model without rendering it: every path, attribute, block and document setting.
        /// Never throws for what the template says; throws only when the package is not an OpenDocument text.
        /// </summary>
        public static TemplateValidation Validate(byte[] document, IShape root) => Validator.Validate(document, root);

        /// <summary>
        /// Renders the template for a model. Every <c>&lt;$path&gt;</c> prints the value at its path, formatted by the settings, the template's
        /// document settings and the tag's attributes; <c>&lt;@if&gt;</c>, <c>&lt;@ifdef&gt;</c> and <c>&lt;@for&gt;</c> keep, drop or repeat
        /// their blocks; a picture frame whose description is <c>&lt;$path&gt;</c> shows the picture at that path, or leaves the document when
        /// there is none. Paths resolve through the settings' resolver, or the template's own when the settings name none. The result carries
        /// the document and the findings: a member the model does not have prints blank and is reported, a loop that cannot be ordered keeps
        /// its order and is reported, a value that is no picture is reported, a tag that cannot be read prints as typed and is reported.
        /// </summary>
        public RenderResult Render(object model, RenderSettings settings = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            settings = settings ?? new RenderSettings();
            var resolver = settings.Resolver ?? this.defaultResolver;
            var formatting = settings.ToFormatting().With(this.documentSettings);

            var pictures = new Pictures(this.fileByFileName.Keys);
            var renderer = new Renderer(resolver, formatting, model, pictures);
            var content = renderer.Render(this.contentProgram);
            var styles = this.stylesProgram == null ? null : renderer.Render(this.stylesProgram);

            var (files, manifest) = pictures.Files(this.fileByFileName, renderer.Hrefs, this.manifest);
            this.fileByFileName.TryGetValue(MimetypeFileName, out var mimetype);
            var package = Package.Save(mimetype, content, styles, manifest, files);
            return new RenderResult(package, renderer.Findings);
        }
    }
}
