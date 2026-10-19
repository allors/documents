// <copyright file="PngWriter.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Barcodes
{
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using ZXing.Common;

    /// <summary>
    /// Writes a bit matrix as a PNG: 8-bit greyscale, a set bit black and a clear bit white, one IDAT chunk whose zlib stream is a deflate
    /// stream between the zlib header and the Adler-32 of the rows. The package needs no imaging library for a barcode.
    /// </summary>
    internal static class PngWriter
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly uint[] CrcTable = MakeCrcTable();

        public static byte[] Write(BitMatrix matrix)
        {
            var width = matrix.Width;
            var height = matrix.Height;
            var stride = width + 1;
            var rows = new byte[height * stride];
            for (var y = 0; y < height; y++)
            {
                var at = y * stride;
                rows[at] = 0;
                for (var x = 0; x < width; x++)
                {
                    rows[at + 1 + x] = matrix[x, y] ? (byte)0 : (byte)255;
                }
            }

            using (var png = new MemoryStream())
            {
                png.Write(Signature, 0, Signature.Length);
                WriteChunk(png, "IHDR", Header(width, height));
                WriteChunk(png, "IDAT", Zlib(rows));
                WriteChunk(png, "IEND", new byte[0]);
                return png.ToArray();
            }
        }

        private static byte[] Header(int width, int height)
        {
            var header = new byte[13];
            BigEndian(header, 0, (uint)width);
            BigEndian(header, 4, (uint)height);
            header[8] = 8;
            header[9] = 0;
            header[10] = 0;
            header[11] = 0;
            header[12] = 0;
            return header;
        }

        private static byte[] Zlib(byte[] rows)
        {
            using (var stream = new MemoryStream())
            {
                stream.WriteByte(0x78);
                stream.WriteByte(0xDA);
                using (var deflate = new DeflateStream(stream, CompressionLevel.Optimal, true))
                {
                    deflate.Write(rows, 0, rows.Length);
                }

                var adler = new byte[4];
                BigEndian(adler, 0, Adler32(rows));
                stream.Write(adler, 0, 4);
                return stream.ToArray();
            }
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var length = new byte[4];
            BigEndian(length, 0, (uint)data.Length);
            stream.Write(length, 0, 4);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes, 0, 4);
            stream.Write(data, 0, data.Length);
            var crc = new byte[4];
            BigEndian(crc, 0, Crc32(typeBytes, data));
            stream.Write(crc, 0, 4);
        }

        private static void BigEndian(byte[] bytes, int at, uint value)
        {
            bytes[at] = (byte)(value >> 24);
            bytes[at + 1] = (byte)(value >> 16);
            bytes[at + 2] = (byte)(value >> 8);
            bytes[at + 3] = (byte)value;
        }

        private static uint Adler32(byte[] bytes)
        {
            uint a = 1;
            uint b = 0;
            foreach (var value in bytes)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }

        private static uint Crc32(byte[] type, byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var value in type)
            {
                crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            foreach (var value in data)
            {
                crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                table[n] = c;
            }

            return table;
        }
    }
}
