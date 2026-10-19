// <copyright file="Pictures.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;

    /// <summary>
    /// The pictures a render puts into the package. A picture is stored once, however many frames show it, under <c>Pictures/&lt;path&gt;.&lt;ext&gt;</c>
    /// with the path of its tag and the extension its bytes say; a second picture at the same path becomes <c>Pictures/&lt;path&gt;.2.&lt;ext&gt;</c>, and so on.
    /// A name the template uses already is skipped. One instance serves one render, across its parts.
    /// </summary>
    internal sealed class Pictures
    {
        private const string Folder = "Pictures/";

        private readonly ICollection<string> templateFiles;
        private readonly Dictionary<string, string> nameByHash = new Dictionary<string, string>();
        private readonly Dictionary<string, int> countByPath = new Dictionary<string, int>();
        private readonly List<Picture> added = new List<Picture>();
        private readonly HashSet<string> names = new HashSet<string>();

        public Pictures(ICollection<string> templateFiles) => this.templateFiles = templateFiles;

        /// <summary>The pictures added so far, in order.</summary>
        public IReadOnlyList<Picture> Added => this.added;

        /// <summary>Stores the picture, unless the same bytes are stored already, and returns the name of its file in the package.</summary>
        public string Add(string path, byte[] bytes, PictureFormat format)
        {
            var hash = Hash(bytes);
            if (this.nameByHash.TryGetValue(hash, out var name))
            {
                return name;
            }

            name = this.NextName(path, format.Extension);
            this.nameByHash.Add(hash, name);
            this.names.Add(name);
            this.added.Add(new Picture(name, bytes, format.MediaType));
            return name;
        }

        /// <summary>
        /// The files of the rendered package and its manifest: the template's files without the pictures no link shows any more, plus the
        /// pictures this render added, each with a manifest entry. Both pass through untouched when nothing changed.
        /// </summary>
        public (IReadOnlyDictionary<string, byte[]> Files, byte[]? Manifest) Files(IReadOnlyDictionary<string, byte[]> template, ICollection<string> hrefs, byte[]? manifest)
        {
            var unreferenced = template.Keys.Where(v => IsPictureFile(v) && !hrefs.Contains(v)).ToList();
            if (unreferenced.Count == 0 && this.added.Count == 0)
            {
                return (template, manifest);
            }

            var files = template.ToDictionary(v => v.Key, v => v.Value);
            var entries = manifest == null ? null : new Manifest(manifest);
            foreach (var name in unreferenced)
            {
                files.Remove(name);
                entries?.Remove(name);
            }

            foreach (var picture in this.added)
            {
                files[picture.Name] = picture.Bytes;
                entries?.Add(picture.Name, picture.MediaType);
            }

            return (files, entries?.ToBytes() ?? manifest);
        }

        private static bool IsPictureFile(string name) => name.StartsWith(Folder, StringComparison.Ordinal) && name.Length > Folder.Length && name[name.Length - 1] != '/';

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        private string NextName(string path, string extension)
        {
            this.countByPath.TryGetValue(path, out var count);
            while (true)
            {
                count++;
                var name = Folder + path + (count == 1 ? string.Empty : "." + count) + "." + extension;
                if (!this.names.Contains(name) && !this.templateFiles.Contains(name))
                {
                    this.countByPath[path] = count;
                    return name;
                }
            }
        }

        /// <summary>A picture in the package: its file name, its bytes and the media type of its manifest entry.</summary>
        public sealed class Picture
        {
            public Picture(string name, byte[] bytes, string mediaType)
            {
                this.Name = name;
                this.Bytes = bytes;
                this.MediaType = mediaType;
            }

            public string Name { get; }

            public byte[] Bytes { get; }

            public string MediaType { get; }
        }
    }
}
