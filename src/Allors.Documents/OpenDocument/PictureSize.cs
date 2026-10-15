// <copyright file="PictureSize.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The size of a picture as its header states it, read for its proportions: pixels for a bitmap, the width and height or else the view box of an
    /// SVG, the frame of a metafile. The unit does not matter, only the ratio does. A header the library cannot read leaves the size unknown.
    /// </summary>
    internal static class PictureSize
    {
        private static readonly Regex SvgAttributeRegex = new Regex(@"\s(width|height|viewBox)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase);

        private static readonly char[] ViewBoxSeparators = { ' ', ',', '\t', '\r', '\n' };

        /// <summary>The picture's width and height, in the format's own unit; false when the bytes do not tell them.</summary>
        public static bool TryRead(PictureFormat format, byte[] bytes, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (format == null || bytes == null)
            {
                return false;
            }

            bool read;
            if (format == PictureFormat.Png)
            {
                read = TryReadPng(bytes, out width, out height);
            }
            else if (format == PictureFormat.Jpeg)
            {
                read = TryReadJpeg(bytes, out width, out height);
            }
            else if (format == PictureFormat.Gif)
            {
                read = TryReadGif(bytes, out width, out height);
            }
            else if (format == PictureFormat.Bmp)
            {
                read = TryReadBmp(bytes, out width, out height);
            }
            else if (format == PictureFormat.WebP)
            {
                read = TryReadWebP(bytes, out width, out height);
            }
            else if (format == PictureFormat.Tiff)
            {
                read = TryReadTiff(bytes, out width, out height);
            }
            else if (format == PictureFormat.Svg)
            {
                read = TryReadSvg(bytes, out width, out height);
            }
            else if (format == PictureFormat.Emf)
            {
                read = TryReadEmf(bytes, out width, out height);
            }
            else if (format == PictureFormat.Wmf)
            {
                read = TryReadWmf(bytes, out width, out height);
            }
            else
            {
                read = false;
            }

            return read && width > 0 && height > 0 && !double.IsInfinity(width) && !double.IsInfinity(height);
        }

        /// <summary>A PNG starts with its IHDR chunk: the width and the height, big-endian, right after the signature and the chunk's length and name.</summary>
        private static bool TryReadPng(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (b.Length < 24 || b[12] != 'I' || b[13] != 'H' || b[14] != 'D' || b[15] != 'R')
            {
                return false;
            }

            width = BigEndian32(b, 16);
            height = BigEndian32(b, 20);
            return true;
        }

        /// <summary>A JPEG states its size in its frame header, SOF0 to SOF15 but for the tables at C4, C8 and CC, which comes before the scan.</summary>
        private static bool TryReadJpeg(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            var at = 2;
            while (at + 3 < b.Length)
            {
                if (b[at] != 0xFF)
                {
                    return false;
                }

                var marker = b[at + 1];
                if (marker == 0xFF)
                {
                    at++;
                    continue;
                }

                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    at += 2;
                    continue;
                }

                if (marker == 0xD9 || marker == 0xDA)
                {
                    return false;
                }

                var length = BigEndian16(b, at + 2);
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    if (length < 7 || at + 8 >= b.Length)
                    {
                        return false;
                    }

                    height = BigEndian16(b, at + 5);
                    width = BigEndian16(b, at + 7);
                    return true;
                }

                at += 2 + length;
            }

            return false;
        }

        /// <summary>A GIF states its logical screen size, little-endian, right after its signature and version.</summary>
        private static bool TryReadGif(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (b.Length < 10)
            {
                return false;
            }

            width = LittleEndian16(b, 6);
            height = LittleEndian16(b, 8);
            return true;
        }

        /// <summary>A bitmap's info header holds its size as 32-bit integers, the height negative for a top-down bitmap; the old core header as 16-bit ones.</summary>
        private static bool TryReadBmp(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (b.Length < 26)
            {
                return false;
            }

            if (LittleEndianInt32(b, 14) == 12)
            {
                width = LittleEndian16(b, 18);
                height = LittleEndian16(b, 20);
            }
            else
            {
                width = LittleEndianInt32(b, 18);
                height = Math.Abs((double)LittleEndianInt32(b, 22));
            }

            return true;
        }

        /// <summary>A WebP's first chunk says its size: a lossy VP8 frame after its start code, a lossless VP8L one in 14-bit fields, an extended VP8X one as a 24-bit canvas.</summary>
        private static bool TryReadWebP(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (b.Length < 25)
            {
                return false;
            }

            switch (Encoding.ASCII.GetString(b, 12, 4))
            {
                case "VP8 ":
                    if (b.Length < 30 || b[23] != 0x9D || b[24] != 0x01 || b[25] != 0x2A)
                    {
                        return false;
                    }

                    width = LittleEndian16(b, 26) & 0x3FFF;
                    height = LittleEndian16(b, 28) & 0x3FFF;
                    return true;

                case "VP8L":
                    if (b[20] != 0x2F)
                    {
                        return false;
                    }

                    var bits = LittleEndian32(b, 21);
                    width = (bits & 0x3FFF) + 1;
                    height = ((bits >> 14) & 0x3FFF) + 1;
                    return true;

                case "VP8X":
                    if (b.Length < 30)
                    {
                        return false;
                    }

                    width = LittleEndian24(b, 24) + 1;
                    height = LittleEndian24(b, 27) + 1;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>A TIFF's first directory holds the ImageWidth (256) and ImageLength (257) entries, as shorts or longs in the file's byte order.</summary>
        private static bool TryReadTiff(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (b.Length < 8)
            {
                return false;
            }

            var little = b[0] == 0x49;
            var directory = (long)Read32(b, 4, little);
            if (directory < 8 || directory + 2 > b.Length)
            {
                return false;
            }

            int count = Read16(b, (int)directory, little);
            for (var i = 0; i < count; i++)
            {
                var entry = directory + 2 + (i * 12);
                if (entry + 12 > b.Length)
                {
                    return false;
                }

                var tag = Read16(b, (int)entry, little);
                if (tag != 0x0100 && tag != 0x0101)
                {
                    continue;
                }

                var type = Read16(b, (int)entry + 2, little);
                double value;
                if (type == 3)
                {
                    value = Read16(b, (int)entry + 8, little);
                }
                else if (type == 4)
                {
                    value = Read32(b, (int)entry + 8, little);
                }
                else
                {
                    return false;
                }

                if (tag == 0x0100)
                {
                    width = value;
                }
                else
                {
                    height = value;
                }

                if (width > 0 && height > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// An SVG's root element states its width and height as lengths, or leaves them to its view box; a relative width or height, such as 100%,
        /// defers to the view box as well. The result is in millimetres for lengths, in user units for a view box.
        /// </summary>
        private static bool TryReadSvg(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;

            var tag = SvgStartTag(Encoding.UTF8.GetString(b, 0, Math.Min(b.Length, 65536)));
            if (tag == null)
            {
                return false;
            }

            string widthText = null;
            string heightText = null;
            string viewBox = null;
            foreach (Match match in SvgAttributeRegex.Matches(tag))
            {
                var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                switch (match.Groups[1].Value.ToLowerInvariant())
                {
                    case "width":
                        widthText = widthText ?? value;
                        break;
                    case "height":
                        heightText = heightText ?? value;
                        break;
                    default:
                        viewBox = viewBox ?? value;
                        break;
                }
            }

            if (Length.TryParse(widthText, "px", out var w, out var widthUnit) && Length.TryParse(heightText, "px", out var h, out var heightUnit))
            {
                width = Length.Convert(w, widthUnit, "mm");
                height = Length.Convert(h, heightUnit, "mm");
                return true;
            }

            var parts = viewBox?.Split(ViewBoxSeparators, StringSplitOptions.RemoveEmptyEntries);
            return parts != null && parts.Length == 4 &&
                   double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out width) &&
                   double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out height);
        }

        /// <summary>An enhanced metafile's header carries its frame in hundredths of a millimetre, and before that its bounds in device units.</summary>
        private static bool TryReadEmf(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (b.Length < 44)
            {
                return false;
            }

            width = (double)LittleEndianInt32(b, 32) - LittleEndianInt32(b, 24);
            height = (double)LittleEndianInt32(b, 36) - LittleEndianInt32(b, 28);
            if (width > 0 && height > 0)
            {
                return true;
            }

            width = (double)LittleEndianInt32(b, 16) - LittleEndianInt32(b, 8) + 1;
            height = (double)LittleEndianInt32(b, 20) - LittleEndianInt32(b, 12) + 1;
            return true;
        }

        /// <summary>Only a placeable metafile states its size: the bounding box of its placeable header, in the units per inch it declares.</summary>
        private static bool TryReadWmf(byte[] b, out double width, out double height)
        {
            width = 0;
            height = 0;
            if (b.Length < 22 || b[0] != 0xD7 || b[1] != 0xCD || b[2] != 0xC6 || b[3] != 0x9A)
            {
                return false;
            }

            width = Math.Abs((double)LittleEndianInt16(b, 10) - LittleEndianInt16(b, 6));
            height = Math.Abs((double)LittleEndianInt16(b, 12) - LittleEndianInt16(b, 8));
            return true;
        }

        /// <summary>The root svg element's start tag, from its bracket to its closing bracket, with the quotes of its attributes respected; null when there is none.</summary>
        private static string SvgStartTag(string text)
        {
            for (var at = text.IndexOf("<svg", StringComparison.Ordinal); at >= 0; at = text.IndexOf("<svg", at + 4, StringComparison.Ordinal))
            {
                var next = at + 4 < text.Length ? text[at + 4] : '\0';
                if (!char.IsWhiteSpace(next) && next != '>' && next != '/' && !(next == ':' && string.CompareOrdinal(text, at + 5, "svg", 0, 3) == 0))
                {
                    continue;
                }

                var quote = '\0';
                for (var i = at; i < text.Length; i++)
                {
                    var c = text[i];
                    if (quote != '\0')
                    {
                        if (c == quote)
                        {
                            quote = '\0';
                        }
                    }
                    else if (c == '"' || c == '\'')
                    {
                        quote = c;
                    }
                    else if (c == '>')
                    {
                        return text.Substring(at, i - at);
                    }
                }

                return null;
            }

            return null;
        }

        private static uint BigEndian32(byte[] b, int at) => ((uint)b[at] << 24) | ((uint)b[at + 1] << 16) | ((uint)b[at + 2] << 8) | b[at + 3];

        private static int BigEndian16(byte[] b, int at) => (b[at] << 8) | b[at + 1];

        private static uint LittleEndian32(byte[] b, int at) => b[at] | ((uint)b[at + 1] << 8) | ((uint)b[at + 2] << 16) | ((uint)b[at + 3] << 24);

        private static int LittleEndian24(byte[] b, int at) => b[at] | (b[at + 1] << 8) | (b[at + 2] << 16);

        private static int LittleEndian16(byte[] b, int at) => b[at] | (b[at + 1] << 8);

        private static int LittleEndianInt32(byte[] b, int at) => (int)LittleEndian32(b, at);

        private static int LittleEndianInt16(byte[] b, int at) => (short)LittleEndian16(b, at);

        private static int Read16(byte[] b, int at, bool little) => little ? LittleEndian16(b, at) : BigEndian16(b, at);

        private static uint Read32(byte[] b, int at, bool little) => little ? LittleEndian32(b, at) : BigEndian32(b, at);
    }
}
