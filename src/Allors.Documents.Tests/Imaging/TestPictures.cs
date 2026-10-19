// <copyright file="TestPictures.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using SkiaSharp;

    /// <summary>Pictures with pixels, built in code: a PNG or JPEG of a size, a JPEG with an orientation, a BMP, and the size of any of them.</summary>
    internal static class TestPictures
    {
        /// <summary>A PNG of the size, opaque unless asked, with a red top-left quarter so that a turn can be told from the pixels.</summary>
        public static byte[] Png(int width, int height, bool transparent = false) => Encode(width, height, SKEncodedImageFormat.Png, transparent);

        /// <summary>A JPEG of the size with a red top-left quarter.</summary>
        public static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg, false);

        /// <summary>A WebP of the size, opaque unless asked, with a red top-left quarter.</summary>
        public static byte[] WebP(int width, int height, bool transparent = false) => Encode(width, height, SKEncodedImageFormat.Webp, transparent);

        /// <summary>
        /// The JPEG with an EXIF orientation: an APP1 segment of a minimal TIFF with the one tag, inserted after the SOI marker,
        /// 1 upright, 3 turned 180 degrees, 6 turned 90 degrees clockwise as a portrait phone photo, 8 counter-clockwise.
        /// </summary>
        public static byte[] WithOrientation(byte[] jpeg, int orientation)
        {
            var tiff = new List<byte> { 0x4D, 0x4D, 0, 42, 0, 0, 0, 8, 0, 1, 0x01, 0x12, 0, 3, 0, 0, 0, 1, 0, (byte)orientation, 0, 0, 0, 0, 0, 0 };
            var app1 = new List<byte> { 0xFF, 0xE1 };
            var length = tiff.Count + 6 + 2;
            app1.Add((byte)(length >> 8));
            app1.Add((byte)length);
            app1.AddRange(new byte[] { (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0 });
            app1.AddRange(tiff);
            var result = new List<byte>(jpeg.Length + app1.Count);
            result.AddRange(new[] { jpeg[0], jpeg[1] });
            result.AddRange(app1);
            for (var i = 2; i < jpeg.Length; i++)
            {
                result.Add(jpeg[i]);
            }

            return result.ToArray();
        }

        /// <summary>A 24-bit BMP of the size, white with a red top-left quarter.</summary>
        public static byte[] Bmp(int width, int height)
        {
            var stride = (width * 3 + 3) / 4 * 4;
            var pixels = stride * height;
            var bytes = new byte[54 + pixels];
            bytes[0] = (byte)'B';
            bytes[1] = (byte)'M';
            LittleEndian(bytes, 2, bytes.Length);
            LittleEndian(bytes, 10, 54);
            LittleEndian(bytes, 14, 40);
            LittleEndian(bytes, 18, width);
            LittleEndian(bytes, 22, height);
            bytes[26] = 1;
            bytes[28] = 24;
            LittleEndian(bytes, 34, pixels);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var at = 54 + (height - 1 - y) * stride + x * 3;
                    var red = x < width / 2 && y < height / 2;
                    bytes[at] = red ? (byte)0 : (byte)255;
                    bytes[at + 1] = red ? (byte)0 : (byte)255;
                    bytes[at + 2] = 255;
                }
            }

            return bytes;
        }

        /// <summary>The pixel size of a picture SkiaSharp can decode, as stored, before any orientation.</summary>
        public static (int Width, int Height) Size(byte[] bytes)
        {
            using (var codec = SKCodec.Create(new MemoryStream(bytes)))
            {
                return (codec.Info.Width, codec.Info.Height);
            }
        }

        /// <summary>The orientation a picture carries.</summary>
        public static SKEncodedOrigin Origin(byte[] bytes)
        {
            using (var codec = SKCodec.Create(new MemoryStream(bytes)))
            {
                return codec.EncodedOrigin;
            }
        }

        /// <summary>True when the pixel is red, as the top-left quarter of every test picture is before it is turned.</summary>
        public static bool IsRed(byte[] bytes, int x, int y)
        {
            using (var bitmap = SKBitmap.Decode(bytes))
            {
                var pixel = bitmap.GetPixel(x, y);
                return pixel.Red > 200 && pixel.Green < 60 && pixel.Blue < 60;
            }
        }

        /// <summary>The 8-bit grey of a pixel: 0 black, 255 white.</summary>
        public static byte Grey(byte[] bytes, int x, int y)
        {
            using (var bitmap = SKBitmap.Decode(bytes))
            {
                return bitmap.GetPixel(x, y).Red;
            }
        }

        private static byte[] Encode(int width, int height, SKEncodedImageFormat format, bool transparent)
        {
            using (var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, transparent ? SKAlphaType.Unpremul : SKAlphaType.Opaque))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(transparent ? SKColors.Transparent : SKColors.White);
                    using (var paint = new SKPaint { Color = SKColors.Red })
                    {
                        canvas.DrawRect(new SKRect(0, 0, width / 2f, height / 2f), paint);
                    }
                }

                using (var image = SKImage.FromBitmap(bitmap))
                using (var encoded = image.Encode(format, 90))
                {
                    return encoded.ToArray();
                }
            }
        }

        private static void LittleEndian(byte[] bytes, int at, int value)
        {
            bytes[at] = (byte)value;
            bytes[at + 1] = (byte)(value >> 8);
            bytes[at + 2] = (byte)(value >> 16);
            bytes[at + 3] = (byte)(value >> 24);
        }
    }
}
