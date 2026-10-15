// <copyright file="Render.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Allors.Document.OpenDocument;
    using Allos.Document.OpenDocument.Tests;

    /// <summary>Renders a body through the resolver engine and reads the result back.</summary>
    internal static class Render
    {
        public static RenderResult Document(string body, object model, RenderSettings settings = null, IDictionary<string, string> documentSettings = null, string styles = null, IDictionary<string, byte[]> pictures = null) =>
            Template(body, documentSettings, styles, pictures).Render(model, settings);

        public static OpenDocumentTemplate Template(string body, IDictionary<string, string> documentSettings = null, string styles = null, IDictionary<string, byte[]> pictures = null) =>
            new OpenDocumentTemplate(Odt.Build(body, pictures, styles, settings: documentSettings), string.Empty);

        public static string Paragraphs(RenderResult result) => Odt.Paragraphs(Odt.Content(result.Document));

        public static string Text(string body, object model, RenderSettings settings = null, IDictionary<string, string> documentSettings = null) =>
            Paragraphs(Document(body, model, settings, documentSettings));

        /// <summary>A paragraph of typed text and tags: a piece that starts with &lt;$ or &lt;@ becomes a placeholder.</summary>
        public static string P(params string[] pieces) =>
            "<text:p>" + string.Concat(pieces.Select(v => v.StartsWith("<$") || v.StartsWith("<@") ? Odt.Placeholder(v) : v)) + "</text:p>";

        public static string Table(params string[] rows) => "<table:table table:name=\"T\">" + string.Concat(rows) + "</table:table>";

        public static string Row(params string[] cells) => "<table:table-row>" + string.Concat(cells.Select(v => "<table:table-cell>" + v + "</table:table-cell>")) + "</table:table-row>";

        /// <summary>A dictionary item with a Name, an Id and whatever else a test needs.</summary>
        public static Dictionary<string, object> Item(string name, int id = 0, string group = null, object[] items = null)
        {
            var item = new Dictionary<string, object> { ["Name"] = name, ["Id"] = id };
            if (group != null)
            {
                item["Group"] = group;
            }

            if (items != null)
            {
                item["Items"] = items;
            }

            return item;
        }
    }
}
