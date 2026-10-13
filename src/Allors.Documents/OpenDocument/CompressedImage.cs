// <copyright file="CompressedImage.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    /// <summary>
    /// Recognizes the picture formats that are compressed already. Deflating them costs time and saves nothing, so the package stores them, as LibreOffice does.
    /// </summary>
    internal static class CompressedImage
    {
        public static bool Is(byte[] bytes)
        {
            if (bytes == null)
            {
                return false;
            }

            return StartsWith(bytes, 0x89, 0x50, 0x4E, 0x47) // PNG
                   || StartsWith(bytes, 0xFF, 0xD8, 0xFF) // JPEG
                   || StartsWith(bytes, 0x47, 0x49, 0x46, 0x38) // GIF
                   || (StartsWith(bytes, 0x52, 0x49, 0x46, 0x46) && bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50); // RIFF....WEBP
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
