// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.PageObjects;

/// <content>XObjects, inline images and shadings.</content>
internal sealed partial class PageContentParser
{
    /// <summary>The bytes of the <c>&lt;&lt;</c> and <c>&gt;&gt;</c> that wrap an inline image's entries.</summary>
    private const int DictionaryDelimiters = 4;

    /// <summary>The width of one delimiter pair.</summary>
    private const int DelimiterWidth = 2;

    /// <summary>The bits of a stencil mask's sample.</summary>
    private const int StencilBits = 1;

    /// <summary>The bits per component an image declares when it gives none.</summary>
    private const int DefaultBits = 8;

    /// <summary>The bounds of the unit square an image is painted into.</summary>
    private static readonly PdfRectangle UnitSquare = new(0, 0, 1, 1);

    /// <summary>Reads a form's /Matrix.</summary>
    /// <param name="dictionary">The form dictionary.</param>
    /// <returns>The matrix, or the identity when it has none.</returns>
    internal static Matrix3x2 ReadMatrix(PdfDictionary dictionary)
    {
        if (dictionary.GetArray(KnownName.Matrix) is not { } array)
        {
            return Matrix3x2.Identity;
        }

        Span<float> values = stackalloc float[SixthOperand + 1];
        return array.ReadNumbers(values) < values.Length
            ? Matrix3x2.Identity
            : new(values[0], values[1], values[ThirdOperand], values[FourthOperand], values[FifthOperand], values[SixthOperand]);
    }

    /// <summary>Handles <c>Do</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpPaintXObject(PageContentParser self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        if (self.FindResource(KnownName.XObject, name).AsStream() is not { } xobject)
        {
            return;
        }

        switch (xobject.Dictionary.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Image:
            {
                self.AddImage(xobject, name);
                break;
            }

            case KnownName.Form:
            {
                self.AddForm(xobject, name);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Handles <c>BI</c> after the reader has found the image's data.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpInlineImage(PageContentParser self, ref ContentReader reader)
    {
        var content = reader.Content;
        var range = reader.InlineImageDictionary;
        var offset = range.Start.GetOffset(content.Length);
        var length = range.End.GetOffset(content.Length) - offset;
        var text = new byte[length + DictionaryDelimiters];
        text[0] = (byte)'<';
        text[1] = (byte)'<';
        content.Slice(offset, length).CopyTo(text.AsSpan(DelimiterWidth));
        text[length + DelimiterWidth] = (byte)'>';
        text[length + DelimiterWidth + 1] = (byte)'>';
        var parser = new PdfParser(text, 0, self._owner.Document.Objects, self._names);
        if (parser.ParseValue().AsDictionary() is not { } dictionary)
        {
            return;
        }

        self.AddInlineImage(dictionary, content[reader.InlineImageData].ToArray());
    }

    /// <summary>Handles <c>sh</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpPaintShading(PageContentParser self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        var item = new PdfShadingObject { Name = name, Dictionary = self.FindResource(KnownName.Shading, name).AsDictionary() };

        // A shading paints the whole clip; without one it covers everything.
        self.Add(item, self._state.Clip?.Bounds ?? PdfPageObject.Unbounded, new(self._operatorStart, self._operatorEnd));
    }

    /// <summary>Expands the abbreviated filter names an inline image may use.</summary>
    /// <param name="name">The filter name.</param>
    /// <returns>The full name.</returns>
    private static PdfName ExpandFilter(PdfName name) => name.ToKnownName() switch
    {
        KnownName.AHx => KnownName.ASCIIHexDecode,
        KnownName.A85 => KnownName.ASCII85Decode,
        KnownName.LZW => KnownName.LZWDecode,
        KnownName.Fl => KnownName.FlateDecode,
        KnownName.RL => KnownName.RunLengthDecode,
        KnownName.CCF => KnownName.CCITTFaxDecode,
        KnownName.DCT => KnownName.DCTDecode,
        _ => name,
    };

    /// <summary>Reads the filter names of an image dictionary.</summary>
    /// <param name="filter">The /Filter value: a name, an array of names, or null.</param>
    /// <returns>The names with abbreviations spelled out.</returns>
    private static PdfName[] ReadFilters(PdfValue filter)
    {
        if (filter.AsArray() is { } array)
        {
            var names = new PdfName[array.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = ExpandFilter(array.GetName(i));
            }

            return names;
        }

        return filter.TryGetName(out var single) ? [ExpandFilter(single)] : [];
    }

    /// <summary>Adds an image XObject.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="name">Its resource name.</param>
    private void AddImage(PdfStream image, PdfName name)
    {
        var dictionary = image.Dictionary;
        var isMask = dictionary.GetBoolean(KnownName.ImageMask);
        var item = new PdfImageObject
        {
            Name = name,
            Stream = image,
            Dictionary = dictionary,
            Width = dictionary.GetInt32(KnownName.Width),
            Height = dictionary.GetInt32(KnownName.Height),
            IsMask = isMask,
            BitsPerComponent = isMask ? StencilBits : dictionary.GetInt32(KnownName.BitsPerComponent, DefaultBits),
            Filters = ReadFilters(dictionary.Get(KnownName.Filter)),
            ColorSpace = dictionary.Get(KnownName.ColorSpace),
        };
        Add(item, TextGeometry.TransformRect(_state.Ctm, UnitSquare), new(_operatorStart, _operatorEnd));
    }

    /// <summary>Adds an inline image.</summary>
    /// <param name="dictionary">The image entries.</param>
    /// <param name="data">The data between <c>ID</c> and <c>EI</c>.</param>
    private void AddInlineImage(PdfDictionary dictionary, byte[] data)
    {
        var isMask = ImageHeader.Get(dictionary, KnownName.ImageMask, KnownName.IM, true).AsBoolean();
        var item = new PdfImageObject
        {
            IsInline = true,
            Dictionary = dictionary,
            InlineData = data,
            Width = ImageHeader.Get(dictionary, KnownName.Width, KnownName.W, true).AsInt32(),
            Height = ImageHeader.Get(dictionary, KnownName.Height, KnownName.H, true).AsInt32(),
            IsMask = isMask,
            BitsPerComponent = isMask ? StencilBits : ImageHeader.Get(dictionary, KnownName.BitsPerComponent, KnownName.BPC, true).AsInt32(DefaultBits),
            Filters = ReadFilters(ImageHeader.Get(dictionary, KnownName.Filter, KnownName.F, true)),
            ColorSpace = ImageHeader.Get(dictionary, KnownName.ColorSpace, KnownName.CS, true),
        };
        Add(item, TextGeometry.TransformRect(_state.Ctm, UnitSquare), new(_operatorStart, _operatorEnd));
    }

    /// <summary>Adds a form XObject.</summary>
    /// <param name="form">The form stream.</param>
    /// <param name="name">Its resource name.</param>
    private void AddForm(PdfStream form, PdfName name)
    {
        var dictionary = form.Dictionary;
        var matrix = ReadMatrix(dictionary);
        PdfRectangle? box = dictionary.TryGetRectangle(KnownName.BBox, out var bbox) ? bbox : null;
        var bounds = box is { } rectangle ? TextGeometry.TransformRect(matrix * _state.Ctm, rectangle) : default;
        var item = new PdfFormObject { Name = name, Stream = form, FormMatrix = matrix, BoundingBox = box };
        Add(item, bounds, new(_operatorStart, _operatorEnd));
    }
}
