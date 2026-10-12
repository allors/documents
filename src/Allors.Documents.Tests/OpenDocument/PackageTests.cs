// <copyright file="PackageTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Allors.Document.OpenDocument;
    using Xunit;

    public class PackageTests
    {
        [Fact]
        public void MimetypeIsTheFirstEntryStoredWithoutAnExtraField()
        {
            var package = Odt.Build("<text:p>body</text:p>");

            var rendered = new OpenDocumentTemplate(package, string.Empty).Render(new Dictionary<string, object>());

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

            var rendered = new OpenDocumentTemplate(package, string.Empty).Render(new Dictionary<string, object>());

            Assert.DoesNotContain(Odt.Entries(rendered), v => v.Name == "mimetype");
            Assert.Equal("body", Odt.Paragraphs(Odt.Content(rendered)));
        }

        [Fact]
        public void LibreOfficeTemplateRendersAValidPackage()
        {
            var model = new Model
            {
                Person = new ModelPerson { FirstName = "Jane" },
                People = [new ModelPerson { FirstName = "John" }, new ModelPerson { FirstName = "Jenny" }],
                Images = ["number1", "number2", "number3"],
            };
            var template = new OpenDocumentTemplate<Model>(Resource("EmbeddedTemplate.odt"));
            var images = new Dictionary<string, byte[]>
            {
                { "logo", Resource("logo.png") },
                { "logo2", Resource("logo.png") },
                { "number1", Resource("1.png") },
                { "number2", Resource("2.png") },
                { "number3", Resource("3.png") },
            };

            var rendered = template.Render(model, images);

            var entries = Odt.Entries(rendered);
            Assert.Equal("mimetype", entries[0].Name);
            Assert.True(entries[0].Stored);
            Assert.Equal(["content.xml", "styles.xml", "META-INF/manifest.xml"], entries.Skip(1).Take(3).Select(v => v.Name));
            Assert.Equal(Resource("1.png"), Odt.Entry(rendered, "Pictures/number1").Bytes);
            Assert.True(Odt.Entry(rendered, "Pictures/number1").Stored);
            Assert.True(Odt.Entry(rendered, "Thumbnails/thumbnail.png").Stored);
            Assert.False(Odt.Entry(rendered, "content.xml").Stored);

            var content = Odt.Content(rendered);
            Assert.Contains("Jane", Odt.Paragraphs(content));
            Assert.Contains(content.Descendants(Odt.Draw + "frame"), v => (string)v.Attribute(Odt.Draw + "name") == "number1");
            Assert.Contains(Odt.Manifest(rendered).Descendants(Odt.ManifestNs + "file-entry"), v => (string)v.Attribute(Odt.ManifestNs + "full-path") == "Pictures/number1");
        }

        [Fact]
        public void AlreadyCompressedPicturesAreStoredAndTheRestIsDeflated()
        {
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var svg = Encoding.ASCII.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><rect width='1' height='1'/></svg>");
            var pictures = new Dictionary<string, byte[]> { ["placeholder.png"] = png, ["drawing.svg"] = svg };
            var package = Odt.Build("<text:p>" + Odt.Frame("$Photo", "Pictures/placeholder.png") + "</text:p>", pictures);

            var rendered = new OpenDocumentTemplate(package, "Photo").Render(new Dictionary<string, object> { ["Photo"] = "photo" }, new Dictionary<string, byte[]> { ["photo"] = jpeg });

            Assert.True(Odt.Entry(rendered, "Pictures/placeholder.png").Stored);
            Assert.True(Odt.Entry(rendered, "Pictures/photo").Stored);
            Assert.False(Odt.Entry(rendered, "Pictures/drawing.svg").Stored);
            Assert.False(Odt.Entry(rendered, "content.xml").Stored);
            Assert.False(Odt.Entry(rendered, "styles.xml").Stored);
        }

        private static byte[] Resource(string name)
        {
            var assembly = typeof(PackageTests).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(v => v.EndsWith("." + name));
            using var resource = assembly.GetManifestResourceStream(resourceName);
            using var output = new MemoryStream();
            resource.CopyTo(output);
            return output.ToArray();
        }
    }
}
