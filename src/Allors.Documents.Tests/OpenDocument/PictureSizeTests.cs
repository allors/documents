// <copyright file="PictureSizeTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Allors.Document.OpenDocument;
    using Xunit;

    /// <summary>
    /// The size a picture's header states, read for its proportions: pixels for a bitmap, millimetres or view box units for an SVG, the frame of a metafile.
    /// </summary>
    public class PictureSizeTests
    {
        public static IEnumerable<object[]> Sizes =>
        [
            ["a PNG", Resource("logo.png"), 392.0, 176.0],
            ["a PNG", Resource("1.png"), 378.0, 370.0],
            ["a baseline JPEG", Jpeg(0xC0, 512, 256), 512.0, 256.0],
            ["a progressive JPEG", Jpeg(0xC2, 640, 480), 640.0, 480.0],
            ["a GIF", Bytes("GIF89a", Le16(300), Le16(200), new byte[3]), 300.0, 200.0],
            ["a top-down bitmap", Bytes("BM", new byte[12], Le32(40), Le32(64), Le32(-32), new byte[4]), 64.0, 32.0],
            ["a bitmap with the core header", Bytes("BM", new byte[12], Le32(12), Le16(64), Le16(32), new byte[8]), 64.0, 32.0],
            ["a lossy WebP", Bytes("RIFF", Le32(0), "WEBPVP8 ", Le32(0), new byte[3], new byte[] { 0x9D, 0x01, 0x2A }, Le16(320 | 0x4000), Le16(240), new byte[2]), 320.0, 240.0],
            ["a lossless WebP", Bytes("RIFF", Le32(0), "WEBPVP8L", Le32(0), new byte[] { 0x2F }, Le32(99 | (49 << 14)), new byte[8]), 100.0, 50.0],
            ["an extended WebP", Bytes("RIFF", Le32(0), "WEBPVP8X", Le32(10), new byte[4], Le24(799), Le24(599), new byte[4]), 800.0, 600.0],
            ["a little-endian TIFF", Bytes("II*\0", Le32(8), Le16(2), Le16(0x100), Le16(3), Le32(1), Le16(640), Le16(0), Le16(0x101), Le16(4), Le32(1), Le32(480), Le32(0)), 640.0, 480.0],
            ["a big-endian TIFF", Bytes("MM\0*", Be32(8), Be16(2), Be16(0x100), Be16(4), Be32(1), Be32(640), Be16(0x101), Be16(3), Be32(1), Be16(480), Be16(0), Be32(0)), 640.0, 480.0],
            ["an SVG with lengths", Svg("width=\"10mm\" height=\"5mm\""), 10.0, 5.0],
            ["an SVG with numbers, which are px", Svg("width=\"96\" height=\"48\""), 25.4, 12.7],
            ["an SVG with mixed units", Svg("stroke-width=\"2\" height=\"1in\" width='72pt'"), 25.4, 25.4],
            ["an SVG with a view box only", Svg("viewBox=\"0 0 30 10\""), 30.0, 10.0],
            ["an SVG with relative lengths and a view box", Svg("width=\"100%\" height=\"100%\" viewBox=\"0,0,16,9\""), 16.0, 9.0],
            ["an SVG with one length and a view box", Svg("width=\"200\" viewBox=\"0 0 4 3\""), 4.0, 3.0],
            ["a prefixed SVG", "<?xml version=\"1.0\"?>\n<!-- logo -->\n<svg:svg xmlns:svg=\"http://www.w3.org/2000/svg\"\n   viewBox='0 0 4 3'>\n</svg:svg>", 4.0, 3.0],
            ["an SVG whose title holds a bracket", Svg("data-title=\"a > b\" width=\"3in\" height=\"1in\""), 76.2, 25.4],
            ["an enhanced metafile", Bytes(Le32(1), Le32(88), Le32(0), Le32(0), Le32(99), Le32(49), Le32(0), Le32(0), Le32(2000), Le32(1000), " EMF", new byte[4]), 2000.0, 1000.0],
            ["an enhanced metafile without a frame", Bytes(Le32(1), Le32(88), Le32(0), Le32(0), Le32(99), Le32(49), Le32(0), Le32(0), Le32(0), Le32(0), " EMF", new byte[4]), 100.0, 50.0],
            ["a placeable metafile", Bytes(new byte[] { 0xD7, 0xCD, 0xC6, 0x9A }, Le16(0), Le16(0), Le16(0), Le16(500), Le16(250), Le16(1440), new byte[6], new byte[4]), 500.0, 250.0],
        ];

        public static IEnumerable<object[]> Unreadable =>
        [
            ["a PNG signature alone", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 }],
            ["a JPEG without a frame header", Bytes(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, Be16(16), "JFIF\0", new byte[9], new byte[] { 0xFF, 0xDA, 0, 2 })],
            ["a truncated JPEG", Bytes(new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8 })],
            ["a JPEG whose height a later marker defines", Jpeg(0xC0, 512, 0)],
            ["a GIF cut short", Bytes("GIF89a", Le16(300))],
            ["a TIFF whose directory lies outside its bytes", Bytes("II*\0", Le32(4096))],
            ["a TIFF with a rational width", Bytes("II*\0", Le32(8), Le16(1), Le16(0x100), Le16(5), Le32(1), Le32(26), Le32(0))],
            ["a WebP with an unknown chunk", Bytes("RIFF", Le32(0), "WEBPALPH", Le32(0), new byte[10])],
            ["an SVG without a size", Svg(string.Empty)],
            ["an SVG with relative lengths and no view box", Svg("width=\"100%\" height=\"1em\"")],
            ["an SVG with a view box of three numbers", Svg("viewBox=\"0 0 30\"")],
            ["an SVG with an empty view box", Svg("viewBox=\"0 0 0 10\"")],
            ["a metafile that is not placeable", Bytes(new byte[] { 0x01, 0x00, 0x09, 0x00 }, new byte[14])],
            ["an enhanced metafile with a frame of nothing", Bytes(Le32(1), Le32(88), Le32(0), Le32(0), Le32(-1), Le32(-1), Le32(0), Le32(0), Le32(0), Le32(0), " EMF", new byte[4])],
        ];

        [Theory]
        [MemberData(nameof(Sizes))]
        public void TheSizeIsReadFromTheHeader(string what, object picture, double width, double height)
        {
            var bytes = picture as byte[] ?? Encoding.UTF8.GetBytes((string)picture);
            var format = PictureFormat.Sniff(bytes);
            Assert.NotNull(format);

            Assert.True(PictureSize.TryRead(format, bytes, out var w, out var h), what);
            Assert.Equal(width, w, 4);
            Assert.Equal(height, h, 4);
        }

        [Theory]
        [MemberData(nameof(Unreadable))]
        public void BytesThatDoNotTellTheSizeLeaveItUnknown(string what, object picture)
        {
            var bytes = picture as byte[] ?? Encoding.UTF8.GetBytes((string)picture);
            var format = PictureFormat.Sniff(bytes);
            Assert.NotNull(format);

            Assert.False(PictureSize.TryRead(format, bytes, out _, out _), what);
        }

        [Fact]
        public void NothingHasNoSize()
        {
            Assert.False(PictureSize.TryRead(null, new byte[0], out _, out _));
            Assert.False(PictureSize.TryRead(PictureFormat.Png, null, out _, out _));
        }

        private static string Svg(string attributes) => "<svg xmlns=\"http://www.w3.org/2000/svg\" " + attributes + "><rect width=\"1\" height=\"1\"/></svg>";

        /// <summary>A JPEG with an APP0 segment, then the frame header with the given marker, then the start of the scan.</summary>
        private static byte[] Jpeg(byte frameMarker, int width, int height) =>
            Bytes(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, Be16(16), "JFIF\0", new byte[9], new byte[] { 0xFF, frameMarker }, Be16(17), new byte[] { 8 }, Be16(height), Be16(width), new byte[] { 3, 1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1 }, new byte[] { 0xFF, 0xDA, 0, 2 });

        private static byte[] Bytes(params object[] parts) => parts.SelectMany(v => v as byte[] ?? Encoding.ASCII.GetBytes((string)v)).ToArray();

        private static byte[] Le16(int value) => [(byte)value, (byte)(value >> 8)];

        private static byte[] Le24(int value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16)];

        private static byte[] Le32(int value) => [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];

        private static byte[] Be16(int value) => [(byte)(value >> 8), (byte)value];

        private static byte[] Be32(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

        private static byte[] Resource(string name)
        {
            var assembly = typeof(PictureSizeTests).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(v => v.EndsWith("." + name));
            using var resource = assembly.GetManifestResourceStream(resourceName);
            using var output = new MemoryStream();
            resource.CopyTo(output);
            return output.ToArray();
        }
    }
}
