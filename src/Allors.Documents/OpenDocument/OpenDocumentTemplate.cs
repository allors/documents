// <copyright file="OpenDocumentTemplate.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// An OpenDocument text with template tags, read once with its settings and rendered any number of times, from any thread. The template
    /// is a package (.odt) or a flat document (.fodt), one XML file with the pictures embedded; a render gives a package, or a flat document
    /// when the settings ask for one. Reading the template prepares its parts and its <see cref="Inspection"/>; a template whose blocks are
    /// unbalanced or would cut a table is read too, says so with <see cref="CanRender"/>, and throws <see cref="TemplateException"/> at <see cref="Render"/>.
    /// A template never changes after it is read: a render keeps its own state, so renders may run concurrently; the settings' resolver
    /// is shared by every render and keeps no state of a render.
    /// </summary>
    public class OpenDocumentTemplate
    {
        internal const string ContentFileName = "content.xml";

        internal const string StylesFileName = "styles.xml";

        internal const string MetaFileName = "META-INF/manifest.xml";

        internal const string MimetypeFileName = "mimetype";

        internal const string DocumentMetaFileName = "meta.xml";

        internal const string SettingsFileName = "settings.xml";

        private readonly Dictionary<string, byte[]> fileByFileName;

        private readonly byte[] manifest;

        private readonly Program contentProgram;

        private readonly Program stylesProgram;

        private readonly IReadOnlyDictionary<string, InspectedMap> maps;

        private readonly IReadOnlyList<Finding> blocking;

        /// <summary>
        /// Reads a template, a package or a flat document, with the settings every render uses; null settings are the library's defaults.
        /// Throws <see cref="InvalidDataException"/> when the bytes are neither an OpenDocument package with a content.xml nor a flat
        /// OpenDocument text. Never throws for what the template says: a block that never ends, an end that opens nothing, an else that
        /// belongs to no condition or a block that would cut a table leave <see cref="CanRender"/> false, and <see cref="Validate"/> reports them.
        /// </summary>
        public OpenDocumentTemplate(byte[] document, RenderSettings settings = null)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            this.Settings = settings ?? new RenderSettings();

            var parts = Parts.Read(document);
            this.fileByFileName = parts.Files;
            this.manifest = parts.Manifest;
            this.Inspection = Inspector.Inspect(parts);

            var maps = new List<InspectedMap>();
            var blocking = new List<Finding>();
            this.contentProgram = Program.TryBuild(ContentFileName, parts.Content, maps, blocking);
            this.stylesProgram = parts.Styles == null ? null : Program.TryBuild(StylesFileName, parts.Styles, maps, blocking);
            this.maps = maps.ToDictionary(v => v.Name);
            this.blocking = blocking;
        }

        /// <summary>The settings every render of this template uses: the resolver, the caller's formatting layer and the output form.</summary>
        public RenderSettings Settings { get; }

        /// <summary>What the template asks of its model, read without a model: the tags, the bound pictures, the maps, the document settings and the problems.</summary>
        public TemplateInspection Inspection { get; }

        /// <summary>
        /// True when every block of the template can be rendered. False when a block never ends, an end opens nothing, an else belongs to
        /// no condition or a block would cut a table: <see cref="Render"/> then throws <see cref="TemplateException"/> with those findings, and
        /// <see cref="Validate"/> reports them.
        /// </summary>
        public bool CanRender => this.blocking.Count == 0;

        /// <summary>The resolver a render reads through when the settings name none: the dictionary resolver.</summary>
        protected virtual IResolver DefaultResolver => DictionaryResolver.Instance;

        /// <summary>
        /// Checks the template against the shape of its model without rendering it: every path, attribute, block and document setting,
        /// under the template's settings. Never throws for what the template says.
        /// </summary>
        public TemplateValidation Validate(IShape root) => Validator.Validate(this.Inspection, root, this.Settings.ToFormatting());

        /// <summary>
        /// Renders the template for a model. Every <c>&lt;$path&gt;</c> prints the value at its path, formatted by the settings, the template's
        /// document settings and the tag's attributes; <c>&lt;@if&gt;</c>, <c>&lt;@ifdef&gt;</c> and <c>&lt;@for&gt;</c> keep, drop or repeat
        /// their blocks, <c>&lt;@else&gt;</c> the branch a condition falls back to; a picture frame whose description is <c>&lt;$path&gt;</c> shows the picture at that path, or leaves the document when
        /// there is none. Paths resolve through the settings' resolver, or the template's own when the settings name none. The result carries
        /// the document, a package unless the settings ask for a flat document, and the findings: a member the model does not have prints blank
        /// and is reported, a loop that cannot be ordered keeps its order and is reported, a value that is no picture is reported, a tag that
        /// cannot be read prints as typed and is reported. Throws <see cref="TemplateException"/> when <see cref="CanRender"/> is false.
        /// </summary>
        public RenderResult Render(object model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (!this.CanRender)
            {
                throw new TemplateException(this.blocking);
            }

            var resolver = this.Settings.Resolver ?? this.DefaultResolver;
            var formatting = this.Settings.ToFormatting().With(this.Inspection.Settings);

            var pictures = new Pictures(this.fileByFileName.Keys);
            var renderer = new Renderer(resolver, formatting, model, pictures, this.maps);
            var content = renderer.Render(this.contentProgram);
            var styles = this.stylesProgram == null ? null : renderer.Render(this.stylesProgram);

            var (files, manifest) = pictures.Files(this.fileByFileName, renderer.Hrefs, this.manifest);
            this.fileByFileName.TryGetValue(MimetypeFileName, out var mimetype);
            var document = this.Settings.Flat ? Flat.Write(mimetype, content, styles, files) : Package.Save(mimetype, content, styles, manifest, files);
            return new RenderResult(document, renderer.Findings);
        }
    }
}
