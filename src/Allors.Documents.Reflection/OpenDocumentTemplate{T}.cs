// <copyright file="OpenDocumentTemplate{T}.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Reflection
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Allors.Documents;

    /// <summary>
    /// A template over a model type. A render sees the public properties of <typeparamref name="T"/> by their exact names, the root that
    /// <see cref="ReflectionShape.Arguments{T}"/> gives Validate, and reads what is below them through <see cref="ReflectionResolver"/>,
    /// unless the settings name another resolver.
    /// </summary>
#if NET
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Reads the members of a type through reflection.")]
    public class OpenDocumentTemplate<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] T> : OpenDocumentTemplate
#else
    public class OpenDocumentTemplate<T> : OpenDocumentTemplate
#endif
    {
        private readonly PropertyInfo[] properties;

        /// <summary>
        /// Reads a template with the settings every render uses; null settings are the library's defaults. Throws
        /// <see cref="System.IO.InvalidDataException"/> when the bytes are not an OpenDocument text; a template whose blocks cannot be
        /// rendered is read, reported by <see cref="OpenDocumentTemplate.Validate"/> and thrown at <see cref="Render"/>.
        /// </summary>
        public OpenDocumentTemplate(byte[] document, RenderSettings settings = null)
            : base(document, settings) =>
            this.properties = typeof(T).GetProperties();

        /// <summary>The resolver a render reads through when the settings name none: the reflection resolver.</summary>
        protected override IResolver DefaultResolver => ReflectionResolver.Instance;

        /// <summary>Renders the template for a model; see <see cref="OpenDocumentTemplate.Render(object)"/>.</summary>
        public RenderResult Render(T model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            // The root is a dictionary of the properties as written: a path starts with a property name in its exact case, as Validate checks it
            var root = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var property in this.properties)
            {
                if (property.GetIndexParameters().Length == 0 && !root.ContainsKey(property.Name))
                {
                    root[property.Name] = property.GetValue(model);
                }
            }

            return this.Render((object)root);
        }
    }
}
