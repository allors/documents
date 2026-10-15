// <copyright file="Engine.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System.Collections.Generic;
    using System.Globalization;
    using Allors.Document.OpenDocument;

    /// <summary>
    /// The two engines a template renders with until 3.0: StringTemplate, which 2.x shipped, and the resolver engine that replaces it.
    /// A test whose expectation holds under both runs as a theory over <see cref="Both"/>.
    /// </summary>
    public abstract class Engine
    {
        public static readonly Engine StringTemplate = new StringTemplateEngine();

        public static readonly Engine Resolver = new ResolverEngine();

        public static IEnumerable<object[]> Both => new[] { new object[] { StringTemplate }, new object[] { Resolver } };

        /// <summary>Renders a dictionary model in the current culture, as 2.x does.</summary>
        public abstract byte[] Render(byte[] package, string arguments, IDictionary<string, object> model);

        public override string ToString() => this.GetType().Name.Replace("Engine", string.Empty);

        private sealed class StringTemplateEngine : Engine
        {
            public override byte[] Render(byte[] package, string arguments, IDictionary<string, object> model) => new OpenDocumentTemplate(package, arguments).Render(model);
        }

        private sealed class ResolverEngine : Engine
        {
            public override byte[] Render(byte[] package, string arguments, IDictionary<string, object> model) =>
                new OpenDocumentTemplate(package, arguments).Render(model, new RenderSettings { Culture = CultureInfo.CurrentCulture }).Document;
        }
    }
}
