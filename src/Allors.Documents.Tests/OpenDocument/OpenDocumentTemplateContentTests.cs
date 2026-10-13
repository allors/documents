// <copyright file="OpenDocumentTemplateContentTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using Allors.Document.OpenDocument;

    using Xunit;

    public class OpenDocumentTemplateContentTests
    {
        [Fact]
        public void ToStringTemplate()
        {
            const char L = '˂';
            const char R = '˃';
            const string Ns = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\"";

            // Writer saves a part without indentation: every white space character in it is the author's
            var input =
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<office:document-content " + Ns + "><office:body><office:text>" +
                "<text:p text:style-name=\"P1\">Dear <text:placeholder text:placeholder-type=\"text\">&lt;$person.FirstName&gt;</text:placeholder>,</text:p>" +
                "<text:p><text:placeholder text:placeholder-type=\"text\">&lt;@if person.FirstName&gt;</text:placeholder></text:p>" +
                "<text:p>Hello<text:span text:style-name=\"T1\"> </text:span>again</text:p>" +
                "<text:p><text:placeholder text:placeholder-type=\"text\">&lt;@end&gt;</text:placeholder></text:p>" +
                "<text:p><text:placeholder text:placeholder-type=\"text\">&lt;@for p people&gt;</text:placeholder></text:p>" +
                "<text:p><text:placeholder text:placeholder-type=\"text\">&lt;$p.FirstName&gt;</text:placeholder></text:p>" +
                "<text:p><text:placeholder text:placeholder-type=\"text\">&lt;@end&gt;</text:placeholder></text:p>" +
                "</office:text></office:body></office:document-content>";

            var document = new OpenDocumentTemplateContent(System.Text.Encoding.UTF8.GetBytes(input), L, R);

            var actual = document.ToStringTemplate();

            // The XML comes back character for character: the tags become expressions, and the paragraphs that held a block tag go
            var expected =
                " ::= <<\n" +
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<office:document-content " + Ns + "><office:body><office:text>" +
                "<text:p text:style-name=\"P1\">Dear " + L + "person.FirstName; format=\"xml-string\"" + R + ",</text:p>" +
                L + "if(person.FirstName)" + R + "<text:p>Hello<text:span text:style-name=\"T1\"> </text:span>again</text:p>" + L + "endif" + R +
                L + "people:{p|" + L + "for1(p)" + R + "}" + R +
                "</office:text></office:body></office:document-content>\n" +
                ">>\n" +
                "\n" +
                "for1(p) ::= <<\n" +
                "<text:p>" + L + "p.FirstName; format=\"xml-string\"" + R + "</text:p>\n" +
                ">>";

            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }
    }
}
