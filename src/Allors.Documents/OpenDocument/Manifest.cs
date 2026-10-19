// <copyright file="Manifest.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Xml;

    /// <summary>
    /// The package's manifest (META-INF/manifest.xml): one entry per file with its media type. Entries come and go with the pictures of a
    /// render; a manifest nothing changed passes through byte for byte.
    /// </summary>
    internal sealed class Manifest
    {
        public const string Namespace = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

        private readonly byte[] original;
        private readonly XmlDocument document;
        private readonly XmlElement root;
        private bool changed;

        public Manifest(byte[] bytes)
        {
            this.original = bytes;
            this.document = Inspector.Load(bytes);
            this.root = Inspector.Root(this.document);
        }

        /// <summary>Drops every entry for the file, with the white space before it.</summary>
        public void Remove(string fullPath)
        {
            foreach (var entry in this.Entries.Where(v => FullPath(v) == fullPath).ToList())
            {
                var before = entry.PreviousSibling;
                this.root.RemoveChild(entry);
                if (IsWhitespace(before))
                {
                    this.root.RemoveChild(before);
                }

                this.changed = true;
            }
        }

        /// <summary>Adds an entry for the file with its media type, unless one exists; it sits after the last entry, indented like it.</summary>
        public void Add(string fullPath, string mediaType)
        {
            if (this.Entries.Any(v => FullPath(v) == fullPath))
            {
                return;
            }

            var prefix = this.root.GetPrefixOfNamespace(Namespace);
            var entry = this.document.CreateElement(prefix, "file-entry", Namespace);
            entry.Attributes.Append(this.Attribute(prefix, "full-path", fullPath));
            entry.Attributes.Append(this.Attribute(prefix, "media-type", mediaType));

            var last = this.Entries.LastOrDefault();
            if (last == null)
            {
                this.root.AppendChild(entry);
            }
            else
            {
                this.root.InsertAfter(entry, last);
                var before = last.PreviousSibling;
                if (IsWhitespace(before))
                {
                    this.root.InsertAfter(before.CloneNode(false), last);
                }
            }

            this.changed = true;
        }

        /// <summary>The manifest as the package holds it: the template's bytes when nothing changed.</summary>
        public byte[] ToBytes() => this.changed ? XmlOutput.Write(writer => this.root.WriteTo(writer)) : this.original;

        private IEnumerable<XmlElement> Entries => this.root.ChildNodes.OfType<XmlElement>().Where(v => v.NamespaceURI == Namespace && v.LocalName == "file-entry");

        private static string FullPath(XmlElement entry) => entry.GetAttribute("full-path", Namespace);

        private static bool IsWhitespace([NotNullWhen(true)] XmlNode? node) => node is XmlWhitespace || node is XmlSignificantWhitespace;

        private XmlAttribute Attribute(string prefix, string localName, string value)
        {
            var attribute = this.document.CreateAttribute(prefix, localName, Namespace);
            attribute.Value = value;
            return attribute;
        }
    }
}
