// <copyright file="Stubs.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allors.Documents.Tests
{
    using System;
    using System.Collections.Generic;
    using Allors.Documents;

    /// <summary>A text renderer for the tests: a name and what it renders, counting its calls.</summary>
    internal sealed class TextRendererStub : ITextRenderer
    {
        private readonly Func<object, string, Formatting, string> render;

        public TextRendererStub(string name, Func<object, string, Formatting, string> render)
        {
            this.Name = name;
            this.render = render;
        }

        public string Name { get; }

        public int Calls { get; private set; }

        public string Render(object value, string text, Formatting formatting)
        {
            this.Calls++;
            return this.render(value, text, formatting);
        }
    }

    /// <summary>A picture renderer for the tests: a name and what it draws, recording the value, the text and the frame of every call.</summary>
    internal sealed class PictureRendererStub : IPictureRenderer
    {
        private readonly Func<object, string, PictureFrame, byte[]> render;

        public PictureRendererStub(string name, Func<object, string, PictureFrame, byte[]> render)
        {
            this.Name = name;
            this.render = render;
        }

        public string Name { get; }

        public List<(object Value, string Text, PictureFrame Frame)> Calls { get; } = new List<(object, string, PictureFrame)>();

        public byte[] Render(object value, string text, PictureFrame frame)
        {
            this.Calls.Add((value, text, frame));
            return this.render(value, text, frame);
        }
    }

    /// <summary>A renderer that serves a binding and a picture frame alike, under one name.</summary>
    internal sealed class BothRendererStub : ITextRenderer, IPictureRenderer
    {
        private readonly Func<object, string, Formatting, string> text;
        private readonly Func<object, string, PictureFrame, byte[]> picture;

        public BothRendererStub(string name, Func<object, string, Formatting, string> text, Func<object, string, PictureFrame, byte[]> picture)
        {
            this.Name = name;
            this.text = text;
            this.picture = picture;
        }

        public string Name { get; }

        public string Render(object value, string text, Formatting formatting) => this.text(value, text, formatting);

        public byte[] Render(object value, string text, PictureFrame frame) => this.picture(value, text, frame);
    }

    /// <summary>A picture processor for the tests: what it gives for the bytes, recording every call.</summary>
    internal sealed class ProcessorStub : IPictureProcessor
    {
        private readonly Func<byte[], PictureFormat, PictureFrame, byte[]> process;

        public ProcessorStub(Func<byte[], PictureFormat, PictureFrame, byte[]> process) => this.process = process;

        public List<(byte[] Bytes, PictureFormat Format, PictureFrame Frame)> Calls { get; } = new List<(byte[], PictureFormat, PictureFrame)>();

        public byte[] Process(byte[] bytes, PictureFormat format, PictureFrame frame)
        {
            this.Calls.Add((bytes, format, frame));
            return this.process(bytes, format, frame);
        }
    }
}
