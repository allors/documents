// <copyright file="Stubs.cs" company="Allors bvba">
// Copyright (c) Allors bvba. All rights reserved.
// Licensed under the LGPL license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Allos.Document.Rendering.Tests
{
    using System;
    using System.Collections.Generic;
    using Allors.Document.Models;
    using Allors.Document.OpenDocument;

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

    /// <summary>A picture renderer for the tests: a name and what it draws, recording the value and the frame of every call.</summary>
    internal sealed class PictureRendererStub : IPictureRenderer
    {
        private readonly Func<object, PictureFrame, byte[]> render;

        public PictureRendererStub(string name, Func<object, PictureFrame, byte[]> render)
        {
            this.Name = name;
            this.render = render;
        }

        public string Name { get; }

        public List<(object Value, PictureFrame Frame)> Calls { get; } = new List<(object, PictureFrame)>();

        public byte[] Render(object value, PictureFrame frame)
        {
            this.Calls.Add((value, frame));
            return this.render(value, frame);
        }
    }

    /// <summary>A renderer that serves a binding and a picture frame alike, under one name.</summary>
    internal sealed class BothRendererStub : ITextRenderer, IPictureRenderer
    {
        private readonly Func<object, string, Formatting, string> text;
        private readonly Func<object, PictureFrame, byte[]> picture;

        public BothRendererStub(string name, Func<object, string, Formatting, string> text, Func<object, PictureFrame, byte[]> picture)
        {
            this.Name = name;
            this.text = text;
            this.picture = picture;
        }

        public string Name { get; }

        public string Render(object value, string text, Formatting formatting) => this.text(value, text, formatting);

        public byte[] Render(object value, PictureFrame frame) => this.picture(value, frame);
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
