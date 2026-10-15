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
    using System.Linq;
    using Allors.Document.Inspection;
    using Allors.Document.Models;
    using Allors.Document.Rendering;
    using Allors.Document.Validation;
    using Allors.Document.Xml;
    using Antlr4.StringTemplate;
    using Antlr4.StringTemplate.Misc;

    public class OpenDocumentTemplate
    {
        internal const string ContentFileName = "content.xml";

        internal const string StylesFileName = "styles.xml";

        internal const string MetaFileName = "META-INF/manifest.xml";

        internal const string MimetypeFileName = "mimetype";

        private const string DocumentMetaFileName = "meta.xml";

        protected const char DefaultLeftDelimiter = '˂';

        protected const char DefaultRightDelimiter = '˃';

        private const string MainTemplateName = "main";

        private readonly Dictionary<string, byte[]> fileByFileName;

        private readonly TemplateGroup ContentTemplateGroup;

        private readonly TemplateGroup StylesTemplateGroup;

        private readonly byte[] manifest;

        private readonly Lazy<Program> contentProgram;

        private readonly Lazy<Program> stylesProgram;

        private readonly Lazy<IReadOnlyDictionary<string, string>> documentSettings;

        public OpenDocumentTemplate(byte[] document, string arguments, char leftDelimiter = DefaultLeftDelimiter, char rightDelimiter = DefaultRightDelimiter)
        {
            this.fileByFileName = new Dictionary<string, byte[]>();

            byte[] contentXml = null;
            byte[] stylesXml = null;

            using (var zip = new MemoryStream(document))
            {
                using (var archive = new ZipArchive(zip, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var zipStream = entry.Open())
                            {
                                zipStream.CopyTo(memoryStream);
                                var file = memoryStream.ToArray();

                                var xmlStringRenderer = new XmlStringRenderer();

                                if (entry.FullName.Equals(ContentFileName))
                                {
                                    contentXml = file;

                                    var errorBuffer = new ErrorBuffer();

                                    var content = new OpenDocumentTemplateContent(file, leftDelimiter, rightDelimiter);
                                    var stringTemplate = content.ToStringTemplate();
                                    var group = MainTemplateName + "(" + arguments + ")" + stringTemplate;
                                    this.ContentTemplateGroup = new TemplateGroupString(MainTemplateName, group, leftDelimiter, rightDelimiter)
                                    {
                                        ErrorManager = new ErrorManager(errorBuffer),
                                    };

                                    // Force a compilation of the templates to check for errors
                                    this.ContentTemplateGroup.GetInstanceOf(MainTemplateName);
                                    if (errorBuffer.Errors.Count > 0)
                                    {
                                        throw new TemplateException(errorBuffer.Errors);
                                    }

                                    this.ContentTemplateGroup.RegisterRenderer(typeof(object), xmlStringRenderer);
                                }
                                else if (entry.FullName.Equals(StylesFileName))
                                {
                                    stylesXml = file;

                                    var errorBuffer = new ErrorBuffer();

                                    var content = new OpenDocumentTemplateContent(file, leftDelimiter, rightDelimiter);
                                    var stringTemplate = content.ToStringTemplate();
                                    var group = MainTemplateName + "(" + arguments + ")" + stringTemplate;
                                    this.StylesTemplateGroup = new TemplateGroupString(MainTemplateName, group, leftDelimiter, rightDelimiter)
                                    {
                                        ErrorManager = new ErrorManager(errorBuffer),
                                    };

                                    // Force a compilation of the templates to check for errors
                                    this.StylesTemplateGroup.GetInstanceOf(MainTemplateName);
                                    if (errorBuffer.Errors.Count > 0)
                                    {
                                        throw new TemplateException(errorBuffer.Errors);
                                    }

                                    this.StylesTemplateGroup.RegisterRenderer(typeof(object), xmlStringRenderer);
                                }
                                else if (entry.FullName.Equals(MetaFileName))
                                {
                                    this.manifest = file;
                                }
                                else
                                {
                                    this.fileByFileName.Add(entry.FullName, file);
                                }
                            }
                        }
                    }
                }
            }

            // The programs of the resolver engine are built on the first Render(object, RenderSettings) and kept; a template that cannot render throws there
            this.contentProgram = new Lazy<Program>(() => contentXml == null ? throw new InvalidDataException("the package has no content.xml: it is not an OpenDocument text") : Program.Build(ContentFileName, contentXml));
            this.stylesProgram = new Lazy<Program>(() => stylesXml == null ? null : Program.Build(StylesFileName, stylesXml));
            this.documentSettings = new Lazy<IReadOnlyDictionary<string, string>>(() => this.fileByFileName.TryGetValue(DocumentMetaFileName, out var meta) ? Inspector.Settings(Inspector.Load(meta)) : new Dictionary<string, string>());
        }

#if NET
        public static string InferArguments([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] Type fromType)
#else
        public static string InferArguments(Type fromType)
#endif
        {
            return string.Join(",", fromType.GetProperties().Select(v => v.Name));
        }

        /// <summary>Reads the tags, bound pictures and document settings of a template without rendering it.</summary>
        public static TemplateInspection Inspect(byte[] document) => Inspector.Inspect(document);

        /// <summary>
        /// Checks a template against the shape of its model without rendering it: every path, attribute, block and document setting.
        /// Never throws for what the template says; throws only when the package is not an OpenDocument text.
        /// </summary>
        public static TemplateValidation Validate(byte[] document, IShape root) => Validator.Validate(document, root);

        public byte[] Render(IDictionary<string, object> model, IDictionary<string, byte[]> imageByName = null)
        {
            var clonedFileByFileName = new Dictionary<string, byte[]>(this.fileByFileName);
            var render = new OpenDocumentRendering(model, this.manifest, imageByName, this.ContentTemplateGroup, this.StylesTemplateGroup, clonedFileByFileName);
            return render.Execute();
        }

        /// <summary>
        /// Renders the template for a model through a resolver. Every <c>&lt;$path&gt;</c> prints the value at its path, formatted by the
        /// settings, the template's document settings and the tag's attributes; <c>&lt;@if&gt;</c>, <c>&lt;@ifdef&gt;</c> and <c>&lt;@for&gt;</c>
        /// keep, drop or repeat their blocks. The result carries the document and the findings: a member the model does not have prints
        /// blank and is reported, a loop that cannot be ordered keeps its order and is reported. Picture frames pass through as they are.
        /// Throws <see cref="TemplateException"/> when a block never ends, an end opens nothing or a block would cut a table.
        /// </summary>
        public RenderResult Render(object model, RenderSettings settings = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            settings = settings ?? new RenderSettings();
            var resolver = settings.Resolver ?? DictionaryResolver.Instance;
            var formatting = settings.ToFormatting().With(this.documentSettings.Value);

            var renderer = new Renderer(resolver, formatting, model);
            var content = renderer.Render(this.contentProgram.Value);
            var styles = this.stylesProgram.Value == null ? null : renderer.Render(this.stylesProgram.Value);

            this.fileByFileName.TryGetValue(MimetypeFileName, out var mimetype);
            var package = Package.Save(mimetype, content, styles, this.manifest, this.fileByFileName);
            return new RenderResult(package, renderer.Findings);
        }
    }
}
