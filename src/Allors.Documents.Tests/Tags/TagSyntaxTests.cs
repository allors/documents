// <copyright file="TagSyntaxTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Tags.Tests
{
    using System.Linq;
    using Allors.Document.Tags;
    using Xunit;

    public class TagSyntaxTests
    {
        [Theory]
        [InlineData("<$Invoice.Number>", "Invoice.Number")]
        [InlineData("<$ line >", "line")]
        [InlineData("  <$Issuer.City>  ", "Issuer.City")]
        [InlineData("<$_private.x1>", "_private.x1")]
        public void BindingsHaveAPath(string text, string path)
        {
            Assert.True(TagSyntax.TryParse(text, out var tag, out var error), error);
            Assert.Equal(TagKind.Binding, tag.Kind);
            Assert.Equal(path, tag.Path);
            Assert.Empty(tag.Attributes);
        }

        [Fact]
        public void BindingsCarryAttributes()
        {
            Assert.True(TagSyntax.TryParse("<$Invoice.Date format=\"yyyy-MM-dd\" culture='en-GB'>", out var tag, out var error), error);

            Assert.Equal("Invoice.Date", tag.Path);
            Assert.Equal("yyyy-MM-dd", tag.Attributes["format"]);
            Assert.Equal("en-GB", tag.Attributes["Culture"]);
            Assert.Equal("<$Invoice.Date format=\"yyyy-MM-dd\" culture='en-GB'>", tag.Text);
        }

        [Theory]
        [InlineData("<@for item Lines>", "item", "Lines")]
        [InlineData("<@FOR t WorkOrder.TimeEntries>", "t", "WorkOrder.TimeEntries")]
        [InlineData("<@For  c   item.SerialisedItemComment >", "c", "item.SerialisedItemComment")]
        public void LoopsHaveAVariableAndAPath(string text, string variable, string path)
        {
            Assert.True(TagSyntax.TryParse(text, out var tag, out var error), error);
            Assert.Equal(TagKind.For, tag.Kind);
            Assert.Equal(variable, tag.Variable);
            Assert.Equal(path, tag.Path);
        }

        [Fact]
        public void LoopsCarrySortKeys()
        {
            Assert.True(TagSyntax.TryParse("<@for line Invoice.Lines sort=\"Product.Name, Quantity desc, Date ASC\">", out var tag, out var error), error);

            Assert.Equal("Invoice.Lines", tag.Path);
            Assert.Equal("line", tag.Variable);
            Assert.Equal("Product.Name, Quantity desc, Date ASC", tag.Attributes["SORT"]);
            Assert.Equal(new[] { "Product.Name", "Quantity desc", "Date" }, tag.Sort.Select(v => v.ToString()));
            Assert.False(tag.Sort[0].Descending);
            Assert.True(tag.Sort[1].Descending);
            Assert.False(tag.Sort[2].Descending);
        }

        [Fact]
        public void LoopsWithoutASortHaveNoKeys()
        {
            Assert.True(TagSyntax.TryParse("<@for item Lines>", out var tag, out _));
            Assert.Empty(tag.Sort);
            Assert.Empty(tag.Attributes);
        }

        [Fact]
        public void UnknownLoopAttributesParseAndAreLeftToValidation()
        {
            Assert.True(TagSyntax.TryParse("<@for item Lines where='x'>", out var tag, out var error), error);
            Assert.Equal("x", tag.Attributes["where"]);
            Assert.Empty(tag.Sort);
        }

        [Theory]
        [InlineData("<@for item Lines sort=\"Name down\">", "expected asc or desc after 'Name', not 'down'")]
        [InlineData("<@for item Lines sort=\"Name,,Date\">", "a key is missing between the commas")]
        [InlineData("<@for item Lines sort=\"\">", "a key is missing between the commas")]
        [InlineData("<@for item Lines sort=\"1st\">", "'1st' is not a path")]
        [InlineData("<@for item Lines sort=\"a b c\">", "expected asc or desc after 'a', not 'b c'")]
        [InlineData("<@for item Lines extra>", "unexpected text after the path")]
        [InlineData("<@for item>", "needs a variable and a path")]
        [InlineData("<@for item  >", "needs a variable and a path")]
        public void BadLoopAttributesAreRefusedWithAReason(string text, string reason)
        {
            Assert.False(TagSyntax.TryParse(text, out var tag, out var error));
            Assert.Null(tag);
            Assert.Contains(reason, error);
        }

        [Theory]
        [InlineData("<@ifdef BillTo.FirstName>", "BillTo.FirstName")]
        [InlineData("<@IFDEF p.Children>", "p.Children")]
        public void DefinedConditionsHaveAPath(string text, string path)
        {
            Assert.True(TagSyntax.TryParse(text, out var tag, out var error), error);
            Assert.Equal(TagKind.IfDef, tag.Kind);
            Assert.Equal(path, tag.Path);
        }

        [Fact]
        public void ADefinedConditionNeedsAPath()
        {
            Assert.False(TagSyntax.TryParse("<@ifdef>", out _, out var error));
            Assert.Equal("<@ifdef> needs a path: <@ifdef Invoice.Comment>", error);
        }

        [Theory]
        [InlineData("<@if Receiver.Contact>", "Receiver.Contact")]
        [InlineData("<@IF BillTo.City>", "BillTo.City")]
        [InlineData("<@If item.HasSerialisedItemComment>", "item.HasSerialisedItemComment")]
        public void ConditionsHaveAPath(string text, string path)
        {
            Assert.True(TagSyntax.TryParse(text, out var tag, out var error), error);
            Assert.Equal(TagKind.If, tag.Kind);
            Assert.Equal(path, tag.Path);
        }

        [Theory]
        [InlineData("<@end>")]
        [InlineData("<@END>")]
        [InlineData("<@end for>")]
        public void EndClosesABlock(string text)
        {
            Assert.True(TagSyntax.TryParse(text, out var tag, out var error), error);
            Assert.Equal(TagKind.End, tag.Kind);
            Assert.Null(tag.Path);
        }

        [Theory]
        [InlineData("<$>")]
        [InlineData("<$a + b>")]
        [InlineData("<$Invoice.Date yyyy-MM-dd>")]
        [InlineData("<$Invoice.Date format=yyyy>")]
        [InlineData("<$1st>")]
        [InlineData("<$Invoice..Number>")]
        [InlineData("<@for Lines>")]
        [InlineData("<@for 1 Lines>")]
        [InlineData("<@foreach x Lines>")]
        [InlineData("<@if>")]
        [InlineData("<@if a b>")]
        [InlineData("<@while x>")]
        public void MalformedTagsAreRefusedWithAReason(string text)
        {
            Assert.False(TagSyntax.TryParse(text, out var tag, out var error));
            Assert.Null(tag);
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Theory]
        [InlineData("Dear customer")]
        [InlineData("<b>")]
        [InlineData("$Logo")]
        [InlineData("<$Invoice.Number")]
        public void PlainTextIsNotATag(string text)
        {
            Assert.False(TagSyntax.IsTag(text));
            Assert.False(TagSyntax.TryParse(text, out _, out _));
        }

        [Fact]
        public void PictureFramesAreBoundByName()
        {
            Assert.True(TagSyntax.TryParsePicture("$Issuer.LogoImage", out var path, out var error), error);
            Assert.Equal("Issuer.LogoImage", path);

            Assert.False(TagSyntax.TryParsePicture("Logo", out _, out _));
            Assert.False(TagSyntax.TryParsePicture("$", out _, out _));
            Assert.False(TagSyntax.TryParsePicture("$item.Photo[0]", out _, out _));
        }
    }
}
