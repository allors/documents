// <copyright file="XmlPairTests.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Xml.Tests
{
    using System;
    using System.Linq;
    using System.Xml;
    using Allors.Document.Xml;
    using Org.XmlUnit.Builder;
    using Xunit;

    public class XmlPairTests
    {
        [Fact]
        public void RewriteSiblingParents()
        {
            const string xml = """

                               <root>
                                 <parent1/>
                                 <parent2/>
                                 <parent3/>
                               </root>

                               """;

            var document = new XmlDocument();
            document.LoadXml(xml);

            var parent1 = (XmlElement)document.GetElementsByTagName("parent1")[0];
            var parent3 = (XmlElement)document.GetElementsByTagName("parent3")[0];

            var rootPair = new XmlPair(null, null);
            var pair = new XmlPair(rootPair, parent1) { End = parent3 };
            rootPair.Children.Add(pair);

            rootPair.Rewrite(v => v.Document.CreateElement("allors"));

            const string expected = """

                                    <root>
                                      <allors>
                                        <parent2/>
                                      </allors>
                                    </root>
                                    """;

            var diff = DiffBuilder.Compare(Input.FromDocument(document)).WithTest(Input.FromString(expected)).Build();

            Assert.False(diff.HasDifferences(), diff.ToString());
        }

        [Fact]
        public void RewriteWithSeparatorsMakesABranchElementPerSeparator()
        {
            const string xml = """

                               <root>
                                 <parent1/>
                                 <parent2/>
                                 <parent3><sep/></parent3>
                                 <parent4/>
                                 <parent5/>
                                 <parent6/>
                                 <parent7/>
                               </root>

                               """;

            var document = new XmlDocument();
            document.LoadXml(xml);

            var parent1 = (XmlElement)document.GetElementsByTagName("parent1")[0];
            var separator = (XmlElement)document.GetElementsByTagName("sep")[0];
            var parent5 = (XmlElement)document.GetElementsByTagName("parent5")[0];
            var parent7 = (XmlElement)document.GetElementsByTagName("parent7")[0];

            var rootPair = new XmlPair(null, null);
            var pair = new XmlPair(rootPair, parent1) { End = parent7 };
            pair.AddSeparator(separator);
            pair.AddSeparator(parent5);
            rootPair.Children.Add(pair);

            Assert.Same(document.DocumentElement, pair.Container);
            Assert.Equal(["parent3", "parent5"], pair.SeparatorSiblings.Select(v => v.LocalName));

            rootPair.Rewrite(v => v.Document.CreateElement("allors"), (v, index) => v.Document.CreateElement("branch" + (index + 1)));

            const string expected = """

                                    <root>
                                      <allors>
                                        <parent2/>
                                        <branch1>
                                          <parent4/>
                                        </branch1>
                                        <branch2>
                                          <parent6/>
                                        </branch2>
                                      </allors>
                                    </root>
                                    """;

            var diff = DiffBuilder.Compare(Input.FromDocument(document)).WithTest(Input.FromString(expected)).Build();
            Assert.False(diff.HasDifferences(), diff.ToString());
        }

        [Fact]
        public void ASeparatorOutsideTheContainerHasNoSibling()
        {
            var document = new XmlDocument();
            document.LoadXml("<root><a><b/><c/></a><d/></root>");

            var rootPair = new XmlPair(null, null);
            var pair = new XmlPair(rootPair, (XmlElement)document.GetElementsByTagName("b")[0]) { End = (XmlElement)document.GetElementsByTagName("c")[0] };
            pair.AddSeparator((XmlElement)document.GetElementsByTagName("d")[0]);
            rootPair.Children.Add(pair);

            Assert.Equal("a", pair.Container.LocalName);
            Assert.Null(Assert.Single(pair.SeparatorSiblings));
            Assert.Throws<InvalidOperationException>(() => rootPair.Rewrite(v => v.Document.CreateElement("x")));
        }

        [Fact]
        public void RewriteNestedSiblingParents()
        {
            const string xml = """

                               <root>
                                 <parent1/>
                                 <parent2/>
                                 <parent3/>
                                 <parent4/>
                                 <parent5/>
                               </root>

                               """;

            var document = new XmlDocument();
            document.LoadXml(xml);

            var rootPair = new XmlPair(null, null);

            var parent1 = (XmlElement)document.GetElementsByTagName("parent1")[0];
            var parent5 = (XmlElement)document.GetElementsByTagName("parent5")[0];
            var pair1 = new XmlPair(rootPair, parent1) { End = parent5 };
            rootPair.Children.Add(pair1);

            var parent2 = (XmlElement)document.GetElementsByTagName("parent2")[0];
            var parent4 = (XmlElement)document.GetElementsByTagName("parent4")[0];
            var pair2 = new XmlPair(rootPair, parent2) { End = parent4 };
            pair1.Children.Add(pair2);

            rootPair.Rewrite(v => v.Document.CreateElement("allors"));

            const string expected = """

                                    <root>
                                      <allors>
                                        <allors>
                                           <parent3/>
                                        </allors>
                                      </allors>
                                    </root>
                                    """;

            var diff = DiffBuilder.Compare(Input.FromDocument(document)).WithTest(Input.FromString(expected)).Build();
            Assert.False(diff.HasDifferences(), diff.ToString());
        }
    }
}
