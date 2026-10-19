// <copyright file="PictureFormat.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// A picture format the library tells from the first bytes of a picture: the extension its file takes, the media type its manifest entry
    /// gets, and whether it is compressed already. Deflating a compressed picture costs time and saves nothing, so the package stores those, as LibreOffice does.
    /// A picture processor gets the format of the bytes it is given and may give bytes in another; <see cref="Sniff"/> tells which.
    /// </summary>
    public sealed class PictureFormat
    {
        public static readonly PictureFormat Png = new PictureFormat("PNG", "png", "image/png", true);
        public static readonly PictureFormat Jpeg = new PictureFormat("JPEG", "jpg", "image/jpeg", true);
        public static readonly PictureFormat Gif = new PictureFormat("GIF", "gif", "image/gif", true);
        public static readonly PictureFormat WebP = new PictureFormat("WebP", "webp", "image/webp", true);
        public static readonly PictureFormat Bmp = new PictureFormat("BMP", "bmp", "image/bmp", false);
        public static readonly PictureFormat Tiff = new PictureFormat("TIFF", "tif", "image/tiff", false);
        public static readonly PictureFormat Svg = new PictureFormat("SVG", "svg", "image/svg+xml", false);
        public static readonly PictureFormat Emf = new PictureFormat("EMF", "emf", "image/x-emf", false);
        public static readonly PictureFormat Wmf = new PictureFormat("WMF", "wmf", "image/x-wmf", false);

        public static readonly IReadOnlyList<PictureFormat> All = new[] { Png, Jpeg, Gif, WebP, Bmp, Tiff, Svg, Emf, Wmf };

        private static readonly int[] BitmapHeaderSizes = { 12, 40, 52, 56, 64, 108, 124 };

        private PictureFormat(string name, string extension, string mediaType, bool isCompressed)
        {
            this.Name = name;
            this.Extension = extension;
            this.MediaType = mediaType;
            this.IsCompressed = isCompressed;
        }

        public string Name { get; }

        public string Extension { get; }

        public string MediaType { get; }

        /// <summary>True when the format compresses its pixels already, so that the package stores the file instead of deflating it.</summary>
        public bool IsCompressed { get; }

        /// <summary>The names of the formats, for a message: PNG, JPEG, ... or WMF.</summary>
        public static string Names => string.Join(", ", All.Take(All.Count - 1).Select(v => v.Name)) + " or " + All[All.Count - 1].Name;

        /// <summary>The format the bytes are in by their signature, or null when they are in none the library knows.</summary>
        public static PictureFormat Sniff(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            if (StartsWith(bytes, 0x89, 0x50, 0x4E, 0x47))
            {
                return Png;
            }

            if (StartsWith(bytes, 0xFF, 0xD8, 0xFF))
            {
                return Jpeg;
            }

            if (StartsWith(bytes, 0x47, 0x49, 0x46, 0x38))
            {
                return Gif;
            }

            if (StartsWith(bytes, 0x52, 0x49, 0x46, 0x46) && bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return WebP;
            }

            if (StartsWith(bytes, 0x42, 0x4D) && bytes.Length >= 26 && Array.IndexOf(BitmapHeaderSizes, BitConverter.ToInt32(bytes, 14)) >= 0)
            {
                return Bmp;
            }

            if (StartsWith(bytes, 0x49, 0x49, 0x2A, 0x00) || StartsWith(bytes, 0x4D, 0x4D, 0x00, 0x2A))
            {
                return Tiff;
            }

            if (StartsWith(bytes, 0x01, 0x00, 0x00, 0x00) && bytes.Length >= 44 && bytes[40] == 0x20 && bytes[41] == 0x45 && bytes[42] == 0x4D && bytes[43] == 0x46)
            {
                return Emf;
            }

            if (StartsWith(bytes, 0xD7, 0xCD, 0xC6, 0x9A) || ((StartsWith(bytes, 0x01, 0x00, 0x09, 0x00) || StartsWith(bytes, 0x02, 0x00, 0x09, 0x00)) && bytes.Length >= 18))
            {
                return Wmf;
            }

            return IsSvg(bytes) ? Svg : null;
        }

        /// <summary>True when the bytes are a picture the package stores instead of deflating.</summary>
        public static bool IsCompressedPicture(byte[] bytes) => Sniff(bytes)?.IsCompressed == true;

        public override string ToString() => this.Name;

        /// <summary>An SVG is text that starts a document and opens an svg element, with or without a prefix, somewhere in its first kilobytes.</summary>
        private static bool IsSvg(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 4096)).TrimStart('﻿', ' ', '\t', '\r', '\n');
            if (text.Length == 0 || text[0] != '<')
            {
                return false;
            }

            for (var at = text.IndexOf("<svg", StringComparison.Ordinal); at >= 0; at = text.IndexOf("<svg", at + 4, StringComparison.Ordinal))
            {
                var next = at + 4 < text.Length ? text[at + 4] : '\0';
                if (char.IsWhiteSpace(next) || next == '>' || next == '/')
                {
                    return true;
                }

                if (next == ':' && string.CompareOrdinal(text, at + 5, "svg", 0, 3) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StartsWith(byte[] bytes, params byte[] signature)
        {
            if (bytes.Length < signature.Length)
            {
                return false;
            }

            for (var i = 0; i < signature.Length; i++)
            {
                if (bytes[i] != signature[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
