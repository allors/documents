// <copyright file="ValueTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.OpenDocument.Tests
{
    using System.Collections.Generic;
    using Xunit;

    /// <summary>Both engines print the values a binding meets the same way.</summary>
    public class ValueTests
    {
        [Theory]
        [MemberData(nameof(Engine.Both), MemberType = typeof(Engine))]
        public void ACollectionBoundAsAValuePrintsItsItemsRunTogether(Engine engine)
        {
            // A model that splits a text into lines and a template that binds the lines with <$> instead of a loop: the lines run together
            var package = Odt.Build("<text:p>" + Odt.Placeholder("<$Lines>") + "</text:p><text:p>" + Odt.Placeholder("<$Counts>") + "</text:p>");
            var model = new Dictionary<string, object> { ["Lines"] = new[] { "first", null, "second" }, ["Counts"] = new object[] { 1, 2 } };

            var rendered = engine.Render(package, "Lines,Counts", model);

            Assert.Equal("firstsecond\n12", Odt.Paragraphs(Odt.Content(rendered)));
        }
    }
}
