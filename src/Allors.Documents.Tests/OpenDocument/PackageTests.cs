// <copyright file="PackageTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Xunit;

    /// <summary>The package the engine writes.</summary>
    public class PackageTests
    {
        [Fact]
        public void MimetypeIsTheFirstEntryStoredWithoutAnExtraField()
        {
            var package = Odt.Build("<text:p>body</text:p>");

            var rendered = Odt.Render(package);

            var first = Odt.Entries(rendered)[0];
            Assert.Equal("mimetype", first.Name);
            Assert.True(first.Stored);
            Assert.Equal(Odt.MediaType, Encoding.ASCII.GetString(first.Bytes));

            // The first local file header: method 0 at offset 8, name and extra field lengths at 26 and 28, so the media type starts at offset 38 (ODF 1.2 part 3, 3.3)
            Assert.Equal(0x04034b50u, BitConverter.ToUInt32(rendered, 0));
            Assert.Equal(0, BitConverter.ToUInt16(rendered, 8));
            Assert.Equal(8, BitConverter.ToUInt16(rendered, 26));
            Assert.Equal(0, BitConverter.ToUInt16(rendered, 28));
            Assert.Equal("mimetype", Encoding.ASCII.GetString(rendered, 30, 8));
            Assert.Equal(Odt.MediaType, Encoding.ASCII.GetString(rendered, 38, Odt.MediaType.Length));
        }

        [Fact]
        public void TemplateWithoutMimetypeStillRenders()
        {
            var package = Odt.Build("<text:p>body</text:p>", withMimetype: false);

            var rendered = Odt.Render(package);

            Assert.DoesNotContain(Odt.Entries(rendered), v => v.Name == "mimetype");
            Assert.Equal("body", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void TemplatePicturesKeepTheirCompression()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var svg = Encoding.ASCII.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><rect width='1' height='1'/></svg>");
            var pictures = new Dictionary<string, byte[]> { ["placeholder.png"] = png, ["drawing.svg"] = svg };
            var package = Odt.Build("<text:p>" + Odt.Frame("Image1", "Pictures/placeholder.png") + Odt.Frame("Image2", "Pictures/drawing.svg") + "</text:p>", pictures);

            var rendered = Odt.Render(package);

            Assert.True(Odt.Entry(rendered, "Pictures/placeholder.png").Stored);
            Assert.False(Odt.Entry(rendered, "Pictures/drawing.svg").Stored);
            Assert.False(Odt.Entry(rendered, "content.xml").Stored);
            Assert.False(Odt.Entry(rendered, "styles.xml").Stored);
            Assert.False(Odt.Entry(rendered, "META-INF/manifest.xml").Stored);
        }
    }
}
