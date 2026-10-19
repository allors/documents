// <copyright file="PictureScaling.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Imaging
{
    using System;
    using System.IO;
    using SkiaSharp;

    /// <summary>
    /// The picture processor that scales a bound picture down to the frame that shows it, for the render settings:
    /// <c>PictureProcessor = new PictureScaling()</c>. A camera photo of 4000 pixels in a frame of 3.96 inches adds megabytes and no detail,
    /// so the processor scales it to the pixels the frame needs at <see cref="Dpi"/>, by the tag's fit: the picture is never scaled up. It applies
    /// the orientation a photo carries to the pixels, so that the document shows the photo upright, and keeps the format: a PNG, and a picture
    /// with transparency, is stored as PNG, every other as JPEG at <see cref="JpegQuality"/>. A picture that fits and is upright passes through
    /// byte for byte, without being decoded; an SVG, EMF, WMF or TIFF and bytes SkiaSharp cannot decode pass through as well.
    /// The processor keeps no state and serves every render of a template, from any thread.
    /// </summary>
    public sealed class PictureScaling : IPictureProcessor
    {
        /// <summary>The resolution the frame prints at: its inches times the dpi are the pixels the picture needs; 300 unless set.</summary>
        public int Dpi { get; init; } = 300;

        /// <summary>The quality of a JPEG the processor writes, 1 to 100; 85 unless set.</summary>
        public int JpegQuality { get; init; } = 85;

        public byte[]? Process(byte[] bytes, PictureFormat format, PictureFrame frame)
        {
            if (bytes == null || bytes.Length == 0 || format == PictureFormat.Svg || format == PictureFormat.Emf || format == PictureFormat.Wmf || format == PictureFormat.Tiff)
            {
                return bytes;
            }

            using (var stream = new MemoryStream(bytes))
            using (var codec = SKCodec.Create(stream))
            {
                if (codec == null)
                {
                    return bytes;
                }

                var origin = codec.EncodedOrigin;
                var swaps = origin == SKEncodedOrigin.LeftTop || origin == SKEncodedOrigin.RightTop || origin == SKEncodedOrigin.RightBottom || origin == SKEncodedOrigin.LeftBottom;
                var info = codec.Info;
                var width = swaps ? info.Height : info.Width;
                var height = swaps ? info.Width : info.Height;
                var scale = this.Scale(width, height, frame);
                var upright = origin == SKEncodedOrigin.TopLeft;
                if (scale >= 1 && upright)
                {
                    return bytes;
                }

                var bitmap = SKBitmap.Decode(codec);
                if (bitmap == null)
                {
                    return bytes;
                }

                try
                {
                    var oriented = Orient(bitmap, origin);
                    if (!ReferenceEquals(oriented, bitmap))
                    {
                        bitmap.Dispose();
                        bitmap = oriented;
                    }

                    if (scale < 1)
                    {
                        var target = new SKImageInfo(Pixels(bitmap.Width, scale), Pixels(bitmap.Height, scale), bitmap.ColorType, bitmap.AlphaType);
                        var scaled = bitmap.Resize(target, new SKSamplingOptions(SKCubicResampler.Mitchell));
                        if (scaled == null)
                        {
                            return bytes;
                        }

                        bitmap.Dispose();
                        bitmap = scaled;
                    }

                    var png = format == PictureFormat.Png || info.AlphaType != SKAlphaType.Opaque;
                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var encoded = image.Encode(png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, this.JpegQuality))
                    {
                        if (encoded == null)
                        {
                            return bytes;
                        }

                        var result = encoded.ToArray();
                        return upright && result.Length >= bytes.Length ? bytes : result;
                    }
                }
                finally
                {
                    bitmap.Dispose();
                }
            }
        }

        public override string ToString() => "PictureScaling " + this.Dpi + " dpi";

        private static int Pixels(int side, double scale) => Math.Max(1, (int)Math.Round(side * scale));

        /// <summary>The scale that gives the frame the pixels it needs by its fit, at most 1: a picture is never scaled up.</summary>
        private double Scale(int width, int height, PictureFrame frame)
        {
            if (frame == null)
            {
                return 1;
            }

            var byWidth = frame.Width > 0 ? frame.Width * this.Dpi / width : (double?)null;
            var byHeight = frame.Height > 0 ? frame.Height * this.Dpi / height : (double?)null;
            if (byWidth == null && byHeight == null)
            {
                return 1;
            }

            double scale;
            switch (frame.Fit)
            {
                case PictureFit.Width:
                    scale = byWidth ?? 1;
                    break;
                case PictureFit.Height:
                    scale = byHeight ?? 1;
                    break;
                case PictureFit.Contain:
                    scale = Math.Min(byWidth ?? double.MaxValue, byHeight ?? double.MaxValue);
                    break;
                default:
                    scale = Math.Max(byWidth ?? 0, byHeight ?? 0);
                    break;
            }

            return Math.Min(1, scale);
        }

        /// <summary>The pixels turned the way the orientation says they are displayed; the bitmap itself when it is upright.</summary>
        private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft)
            {
                return bitmap;
            }

            var width = bitmap.Width;
            var height = bitmap.Height;
            var swaps = origin == SKEncodedOrigin.LeftTop || origin == SKEncodedOrigin.RightTop || origin == SKEncodedOrigin.RightBottom || origin == SKEncodedOrigin.LeftBottom;
            var target = new SKBitmap(swaps ? height : width, swaps ? width : height, bitmap.ColorType, bitmap.AlphaType);
            using (var canvas = new SKCanvas(target))
            {
                // Canvas transforms pre-concatenate: the last call applies to the source pixels first.
                switch (origin)
                {
                    case SKEncodedOrigin.TopRight:
                        canvas.Translate(width, 0);
                        canvas.Scale(-1, 1);
                        break;
                    case SKEncodedOrigin.BottomRight:
                        canvas.Translate(width, height);
                        canvas.RotateDegrees(180);
                        break;
                    case SKEncodedOrigin.BottomLeft:
                        canvas.Translate(0, height);
                        canvas.Scale(1, -1);
                        break;
                    case SKEncodedOrigin.LeftTop:
                        canvas.RotateDegrees(90);
                        canvas.Scale(1, -1);
                        break;
                    case SKEncodedOrigin.RightTop:
                        canvas.Translate(height, 0);
                        canvas.RotateDegrees(90);
                        break;
                    case SKEncodedOrigin.RightBottom:
                        canvas.Translate(height, width);
                        canvas.Scale(-1, 1);
                        canvas.RotateDegrees(-90);
                        break;
                    case SKEncodedOrigin.LeftBottom:
                        canvas.Translate(0, width);
                        canvas.RotateDegrees(-90);
                        break;
                }

                canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            }

            return target;
        }
    }
}
