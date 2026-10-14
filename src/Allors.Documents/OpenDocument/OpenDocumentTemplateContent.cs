// <copyright file="OpenDocumentTemplateContent.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Document.OpenDocument
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Xml;

    using Allors.Document.Tags;
    using Allors.Document.Xml;

    /// <summary>
    /// Turns a part of the package (content.xml or styles.xml) into a StringTemplate group.
    /// The XML is written back character for character: Writer saves a part without indentation, so every white space character in it is the author's.
    /// </summary>
    public class OpenDocumentTemplateContent
    {
        private readonly XmlDocument template;

        private readonly Dictionary<XmlElement, Binding> bindingByXmlElement;
        private readonly Dictionary<XmlElement, Binding> imageBindingByXmlElement;
        private readonly Dictionary<XmlElement, Statement> statementByXmlElement;

        public OpenDocumentTemplateContent(byte[] content, char leftDelimiter, char rightDelimiter)
        {
            this.LeftDelimiter = leftDelimiter;
            this.RightDelimiter = rightDelimiter;

            this.bindingByXmlElement = new Dictionary<XmlElement, Binding>();
            this.imageBindingByXmlElement = new Dictionary<XmlElement, Binding>();
            this.statementByXmlElement = new Dictionary<XmlElement, Statement>();

            // A span that holds only a space is a space the author typed
            this.template = new XmlDocument { PreserveWhitespace = true };
            using (var input = new MemoryStream(content))
            {
                this.template.Load(input);
            }

            var root = new XmlPair(null, null);
            var stack = new Stack<XmlPair>();
            var documentElement = this.template.DocumentElement;

            var walker = root;
            var bindingByOriginalXmlElement = new Dictionary<XmlElement, Binding>();
            this.Prepare(documentElement, ref walker, stack, bindingByOriginalXmlElement);

            var forCounter = 0;
            XmlElement Factory(XmlPair pair)
            {
                var text = pair.Begin.InnerText.Trim();
                TagSyntax.TryParse(text, out var tag, out _);

                if (tag?.Kind == TagKind.For)
                {
                    var element = pair.Document.CreateElement("for");
                    var statement = new For(++forCounter, tag.Variable, tag.Path);
                    this.statementByXmlElement.Add(element, statement);
                    return element;
                }

                // The StringTemplate engine cannot tell a missing member from a null one, so <@ifdef> renders as <@if> until 3.0
                if (tag?.Kind == TagKind.If || tag?.Kind == TagKind.IfDef)
                {
                    var element = pair.Document.CreateElement("if");
                    var statement = new If(tag.Path);
                    this.statementByXmlElement.Add(element, statement);
                    return element;
                }

                throw new Exception("Unknown statement: " + pair.Begin.InnerText);
            }

            foreach (var pair in bindingByOriginalXmlElement)
            {
                var xmlElement = pair.Key;
                var binding = pair.Value;

                var bindingElement = this.template.CreateElement("binding");
                var parent = xmlElement.ParentNode;
                parent.InsertBefore(bindingElement, xmlElement);
                parent.RemoveChild(xmlElement);

                this.bindingByXmlElement.Add(bindingElement, binding);
            }

            root.Rewrite(Factory);
        }

        private char LeftDelimiter { get; }

        private char RightDelimiter { get; }

        /// <summary>
        /// The group: a main template with the part's XML, and one template per for-loop with the loop's body.
        /// StringTemplate drops the newline right after a template's <c>&lt;&lt;</c> and the one right before its <c>&gt;&gt;</c>; the latter keeps a closing <c>&gt;</c> of the XML from forming <c>&gt;&gt;</c> with the terminator.
        /// </summary>
        public string ToStringTemplate()
        {
            using (var writer = new StringWriter())
            {
                writer.Write(@" ::= <<
<?xml version=""1.0"" encoding=""UTF-8""?>
");
                this.Write(this.template.DocumentElement, writer);

                writer.Write(@"
>>");

                foreach (var keyPair in this.statementByXmlElement)
                {
                    var element = keyPair.Key;
                    var statement = keyPair.Value;

                    if (statement is For forStatement)
                    {
                        writer.Write(@"

");
                        writer.Write(forStatement.Statement);
                        writer.Write(@" ::= <<
");

                        this.Write(element.ChildNodes, writer);

                        writer.Write(@"
>>");
                    }
                }

                return writer.ToString();
            }
        }

        private void Prepare(XmlElement parent, ref XmlPair parentPair, Stack<XmlPair> stack, Dictionary<XmlElement, Binding> bindingByOriginalXmlElement)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement element)
                {
                    if (element.LocalName.Equals("placeholder") && element.NamespaceURI.Contains("opendocument:xmlns:text"))
                    {
                        // A placeholder that is not a tag, or a tag that cannot be read, stays as the text the author typed; Validate reports it
                        if (TagSyntax.TryParse(element.InnerText.Trim(), out var tag, out _))
                        {
                            switch (tag.Kind)
                            {
                                case TagKind.Binding:
                                    bindingByOriginalXmlElement[element] = new Binding(tag.Path);
                                    break;

                                case TagKind.For:
                                case TagKind.If:
                                case TagKind.IfDef:
                                    var childNode = new XmlPair(parentPair, element);
                                    stack.Push(childNode);
                                    parentPair.Children.Add(childNode);
                                    parentPair = childNode;
                                    break;

                                case TagKind.End:
                                    var block = stack.Pop();
                                    block.End = element;
                                    parentPair = block.Parent;
                                    break;
                            }
                        }
                    }

                    // Images: a frame named $expression takes the name of the image to show from the model
                    if (element.LocalName.Equals("frame") && element.NamespaceURI.Contains("opendocument:xmlns:drawing"))
                    {
                        var drawNs = element.GetNamespaceOfPrefix("draw");
                        var name = element.GetAttribute("name", drawNs).Trim();
                        if (name.StartsWith("$"))
                        {
                            this.imageBindingByXmlElement[element] = new Binding(name.Substring(1));
                        }
                    }

                    this.Prepare(element, ref parentPair, stack, bindingByOriginalXmlElement);
                }
            }
        }

        private void Write(XmlElement element, TextWriter writer)
        {
            if (this.bindingByXmlElement.TryGetValue(element, out var binding))
            {
                this.WriteBinding(binding, XmlStringRenderer.XmlStringFormat, writer);
                return;
            }

            if (this.statementByXmlElement.TryGetValue(element, out var statement))
            {
                if (statement is If ifStatement)
                {
                    writer.Write(this.LeftDelimiter);
                    writer.Write("if(");
                    writer.Write(ifStatement.Text);
                    writer.Write(")");
                    writer.Write(this.RightDelimiter);

                    this.Write(element.ChildNodes, writer);

                    writer.Write(this.LeftDelimiter);
                    writer.Write("endif");
                    writer.Write(this.RightDelimiter);
                }
                else if (statement is For forStatement)
                {
                    writer.Write(this.LeftDelimiter);
                    writer.Write(forStatement.Expression);
                    writer.Write(":{");
                    writer.Write(forStatement.Argument);
                    writer.Write("|");
                    writer.Write(this.LeftDelimiter);
                    writer.Write(forStatement.Statement);
                    writer.Write(this.RightDelimiter);
                    writer.Write("}");
                    writer.Write(this.RightDelimiter);
                }
                else
                {
                    throw new Exception("Unknown statement: " + statement.GetType() + " - " + statement);
                }

                return;
            }

            this.imageBindingByXmlElement.TryGetValue(element, out var imageBinding);

            writer.Write("<");
            if (!string.IsNullOrWhiteSpace(element.Prefix))
            {
                writer.Write(element.Prefix);
                writer.Write(":");
            }

            writer.Write(element.LocalName);

            foreach (XmlAttribute attribute in element.Attributes)
            {
                writer.Write(" ");
                if (!string.IsNullOrWhiteSpace(attribute.Prefix))
                {
                    writer.Write(attribute.Prefix);
                    writer.Write(":");
                }

                writer.Write(attribute.LocalName);
                writer.Write(@"=""");

                if (imageBinding != null && attribute.LocalName.Equals("name") && attribute.NamespaceURI.Equals(element.NamespaceURI))
                {
                    this.WriteBinding(imageBinding, XmlStringRenderer.XmlAttributeFormat, writer);
                }
                else
                {
                    writer.Write(this.EscapeAttribute(attribute.Value));
                }

                writer.Write(@"""");
            }

            writer.Write(">");

            this.Write(element.ChildNodes, writer);

            writer.Write("</");
            if (!string.IsNullOrWhiteSpace(element.Prefix))
            {
                writer.Write(element.Prefix);
                writer.Write(":");
            }

            writer.Write(element.LocalName);
            writer.Write(">");
        }

        private void Write(XmlNodeList childNodes, TextWriter writer)
        {
            foreach (XmlNode childNode in childNodes)
            {
                switch (childNode.NodeType)
                {
                    case XmlNodeType.Element:
                        this.Write((XmlElement)childNode, writer);
                        break;

                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        writer.Write(this.EscapeText(childNode.Value));
                        break;

                    // TODO: Other XmlNodeTypes

                    // Ignore the following XmlNodeTypes
                    case XmlNodeType.EndElement:
                        break;
                }
            }
        }

        private void WriteBinding(Binding binding, string format, TextWriter writer)
        {
            writer.Write(this.LeftDelimiter);
            writer.Write(binding.Text);
            writer.Write("; format=\"" + format + "\"");
            writer.Write(this.RightDelimiter);
        }

        /// <summary>
        /// The XML reader decoded the character references of the template; the template text is XML again, so they go back in.
        /// StringTemplate then reads that text: a backslash is its escape character and the left delimiter opens an expression, so both get a backslash.
        /// </summary>
        private string EscapeText(string value) => this.EscapeTemplate(value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"));

        private string EscapeAttribute(string value) => this.EscapeTemplate(value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;"));

        private string EscapeTemplate(string value)
        {
            if (value.IndexOf('\\') < 0 && value.IndexOf(this.LeftDelimiter) < 0)
            {
                return value;
            }

            var stringBuilder = new StringBuilder(value.Length + 8);
            foreach (var chr in value)
            {
                if (chr == '\\' || chr == this.LeftDelimiter)
                {
                    stringBuilder.Append('\\');
                }

                stringBuilder.Append(chr);
            }

            return stringBuilder.ToString();
        }
    }
}
