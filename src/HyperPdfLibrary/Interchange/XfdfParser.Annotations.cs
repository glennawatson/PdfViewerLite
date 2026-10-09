// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Interchange;

/// <content>The <c>annots</c> element.</content>
internal sealed partial class XfdfParser
{
    /// <summary>The annotation being read.</summary>
    private PdfInterchangeAnnotation? _annotation;

    /// <summary>The strokes of the ink list being read.</summary>
    private List<float[]>? _strokes;

    /// <summary>The attached file's name, from the <c>file</c> attribute.</summary>
    private string? _fileName;

    /// <summary>The attached file's description.</summary>
    private string? _fileDescription;

    /// <summary>Wraps strokes as memory.</summary>
    /// <param name="strokes">The strokes.</param>
    /// <returns>One entry per stroke.</returns>
    private static ReadOnlyMemory<ReadOnlyMemory<float>> ToMemory(List<float[]> strokes)
    {
        var memory = new ReadOnlyMemory<float>[strokes.Count];
        for (var i = 0; i < memory.Length; i++)
        {
            memory[i] = strokes[i];
        }

        return memory;
    }

    /// <summary>Reads a child of <c>annots</c>.</summary>
    /// <param name="name">The child's name.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    private bool ReadAnnotation(string name)
    {
        if (!XfdfNames.TryGetSubtype(name, out var subtype))
        {
            return false;
        }

        var annotation = new PdfInterchangeAnnotation(subtype);
        _annotation = annotation;
        _fileName = null;
        _fileDescription = null;
        ReadAnnotationAttributes(annotation);
        ReadChildren(static (parser, child) => parser.ReadAnnotationChild(child));
        _data.Annotations.Add(annotation);
        _annotation = null;
        return true;
    }

    /// <summary>Reads the attributes of the element the reader is on into an annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    private void ReadAnnotationAttributes(PdfInterchangeAnnotation annotation)
    {
        if (!_reader.MoveToFirstAttribute())
        {
            return;
        }

        do
        {
            var name = _reader.LocalName;
            if (string.Equals(name, "file", StringComparison.OrdinalIgnoreCase))
            {
                _fileName = _reader.Value;
            }
            else if (string.Equals(name, "description", StringComparison.OrdinalIgnoreCase))
            {
                _fileDescription = _reader.Value;
            }
            else
            {
                _ = XfdfAttributes.Apply(annotation, name, _reader.Value);
            }
        }
        while (_reader.MoveToNextAttribute());
        _ = _reader.MoveToElement();
    }

    /// <summary>Reads a child of an annotation element.</summary>
    /// <param name="name">The child's name.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    private bool ReadAnnotationChild(string name)
    {
        var annotation = _annotation!;
        switch (name.ToLowerInvariant())
        {
            case XfdfNames.Contents:
            {
                annotation.Contents = _reader.ReadElementContentAsString();
                return true;
            }

            case XfdfNames.ContentsRichText:
            {
                annotation.RichContents = NullIfEmpty(_reader.ReadInnerXml());
                return true;
            }

            case XfdfNames.DefaultAppearance:
            {
                annotation.DefaultAppearance = _reader.ReadElementContentAsString();
                return true;
            }

            case XfdfNames.DefaultStyle:
            {
                annotation.DefaultStyle = _reader.ReadElementContentAsString();
                return true;
            }

            case XfdfNames.Popup:
            {
                annotation.Popup = ReadPopup(annotation);
                return false;
            }

            case XfdfNames.InkList:
            {
                ReadInkList(annotation);
                return true;
            }

            case XfdfNames.Vertices:
            {
                annotation.Vertices = InterchangeValues.ParseList(_reader.ReadElementContentAsString());
                return true;
            }

            case XfdfNames.Data:
            {
                annotation.Attachment = ReadAttachment();
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Reads a <c>popup</c> element's attributes.</summary>
    /// <param name="annotation">The parent annotation.</param>
    /// <returns>The pop-up, or <see langword="null"/> when it has no rectangle.</returns>
    private PdfInterchangePopup? ReadPopup(PdfInterchangeAnnotation annotation)
    {
        if (!InterchangeValues.TryParseRect(_reader.GetAttribute("rect"), out var rectangle))
        {
            return null;
        }

        var page = XfdfAttributes.ParseInt(_reader.GetAttribute("page") ?? string.Empty) ?? annotation.Page;
        return new(page, rectangle, InterchangeValues.ParseBoolean(_reader.GetAttribute("open")) ?? false);
    }

    /// <summary>Reads the strokes of an <c>inklist</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    private void ReadInkList(PdfInterchangeAnnotation annotation)
    {
        var saved = _strokes;
        var strokes = new List<float[]>();
        _strokes = strokes;
        ReadChildren(static (parser, child) => parser.ReadGesture(child));
        _strokes = saved;
        annotation.Gestures = ToMemory(strokes);
    }

    /// <summary>Reads a child of <c>inklist</c>.</summary>
    /// <param name="name">The child's name.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    private bool ReadGesture(string name)
    {
        if (!string.Equals(name, XfdfNames.Gesture, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _strokes!.Add(InterchangeValues.ParseList(_reader.ReadElementContentAsString()));
        return true;
    }

    /// <summary>Reads a <c>data</c> element holding an attached file.</summary>
    /// <returns>The attachment; <see langword="null"/> when the bytes cannot be read.</returns>
    private PdfInterchangeAttachment? ReadAttachment()
    {
        var encoding = _reader.GetAttribute("encoding");
        var filter = _reader.GetAttribute("filter");
        var mime = _reader.GetAttribute("MIME-type");
        var text = _reader.ReadElementContentAsString();
        var data = string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase)
            ? InterchangeBytes.DecodeBase64(text)
            : InterchangeBytes.DecodeHex(text);
        if (data is null)
        {
            return null;
        }

        if (string.Equals(filter, "FlateDecode", StringComparison.OrdinalIgnoreCase))
        {
            data = InterchangeBytes.Inflate(data);
        }

        return new(_fileName ?? string.Empty, data, _fileDescription, mime);
    }
}
