// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

using HyperPdfLibrary.Content;

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Reads image, form, inline-image and shading operators.</summary>
internal static class PageContentParseXObjects
{
    /// <summary>The bytes of the <c>&lt; &lt;</c> and <c>&gt; &gt;</c> that wrap an inline image's entries.</summary>
    internal const int DictionaryDelimiters = 4;

    /// <summary>The width of one delimiter pair.</summary>
    internal const int DelimiterWidth = 2;

    /// <summary>The bits of a stencil mask's sample.</summary>
    internal const int StencilBits = 1;

    /// <summary>The bits per component an image declares when it gives none.</summary>
    internal const int DefaultBits = 8;

    /// <summary>The bounds of the unit square an image is painted into.</summary>
    internal static readonly PdfRectangle UnitSquare = new(0, 0, 1, 1);

    /// <summary>Reads a form's /Matrix.</summary>
    /// <param name = "dictionary">The form dictionary.</param>
    /// <returns>The matrix, or the identity when it has none.</returns>
    internal static Matrix3x2 ReadMatrix(PdfDictionary dictionary)
    {
        if (dictionary.GetArray(KnownName.Matrix) is not { } array)
        {
            return Matrix3x2.Identity;
        }

        Span<float> values = stackalloc float[PageContentParse.SixthOperand + 1];
        return array.ReadNumbers(values) < values.Length ? Matrix3x2.Identity : new(
            values[0],
            values[1],
            values[PageContentParse.ThirdOperand],
            values[PageContentParse.FourthOperand],
            values[PageContentParse.FifthOperand],
            values[PageContentParse.SixthOperand]);
    }

    /// <summary>Handles <c>Do</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpPaintXObject(PageContentParseState self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        if (PageContentParseObjects.FindResource(self, KnownName.XObject, name).AsStream() is not { } xobject)
        {
            return;
        }

        switch (xobject.Dictionary.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Image:
                {
                    PageContentParseXObjects.AddImage(self, xobject, name);
                    break;
                }

            case KnownName.Form:
                {
                    PageContentParseXObjects.AddForm(self, xobject, name);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Handles <c>BI</c> after the reader has found the image's data.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpInlineImage(PageContentParseState self, ref ContentReader reader)
    {
        var content = reader.Content;
        var range = reader.InlineImageDictionary;
        var offset = range.Start.GetOffset(content.Length);
        var length = range.End.GetOffset(content.Length) - offset;
        var text = new byte[length + PageContentParseXObjects.DictionaryDelimiters];
        text[0] = (byte)'<';
        text[1] = (byte)'<';
        content.Slice(offset, length).CopyTo(text.AsSpan(PageContentParseXObjects.DelimiterWidth));
        text[length + PageContentParseXObjects.DelimiterWidth] = (byte)'>';
        text[length + PageContentParseXObjects.DelimiterWidth + 1] = (byte)'>';
        var parser = new PdfParser(text, 0, self.Owner.Document.Objects, self.Names);
        if (parser.ParseValue().AsDictionary() is not { } dictionary)
        {
            return;
        }

        PageContentParseXObjects.AddInlineImage(self, dictionary, content[reader.InlineImageData].ToArray());
    }

    /// <summary>Handles <c>sh</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpPaintShading(PageContentParseState self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        var item = new PdfShadingObject { Name = name, Dictionary = PageContentParseObjects.FindResource(self, KnownName.Shading, name).AsDictionary(), };

        // A shading paints the whole clip; without one it covers everything.
        PageContentParseObjects.Add(self, item, self.State.Clip?.Bounds ?? PdfPageObject.Unbounded, new(self.OperatorStart, self.OperatorEnd));
    }

    /// <summary>Expands the abbreviated filter names an inline image may use.</summary>
    /// <param name = "name">The filter name.</param>
    /// <returns>The full name.</returns>
    internal static PdfName ExpandFilter(PdfName name) => name.ToKnownName() switch
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
    /// <param name = "filter">The /Filter value: a name, an array of names, or null.</param>
    /// <returns>The names with abbreviations spelled out.</returns>
    internal static PdfName[] ReadFilters(PdfValue filter)
    {
        if (filter.AsArray() is { } array)
        {
            var names = new PdfName[array.Count];
            for (var i = 0; i < names.Length; i++)
            {
                names[i] = PageContentParseXObjects.ExpandFilter(array.GetName(i));
            }

            return names;
        }

        return filter.TryGetName(out var single) ? [PageContentParseXObjects.ExpandFilter(single)] : [];
    }

    /// <summary>Adds an image XObject.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "image">The image stream.</param>
    /// <param name = "name">Its resource name.</param>
    internal static void AddImage(PageContentParseState state, PdfStream image, PdfName name)
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
            BitsPerComponent = isMask ? PageContentParseXObjects.StencilBits : dictionary.GetInt32(KnownName.BitsPerComponent, PageContentParseXObjects.DefaultBits),
            Filters = PageContentParseXObjects.ReadFilters(dictionary.Get(KnownName.Filter)),
            ColorSpace = dictionary.Get(KnownName.ColorSpace),
        };
        PageContentParseObjects.Add(state, item, TextGeometry.TransformRect(state.State.Ctm, PageContentParseXObjects.UnitSquare), new(state.OperatorStart, state.OperatorEnd));
    }

    /// <summary>Adds an inline image.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "dictionary">The image entries.</param>
    /// <param name = "data">The data between <c>ID</c> and <c>EI</c>.</param>
    internal static void AddInlineImage(PageContentParseState state, PdfDictionary dictionary, byte[] data)
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
            BitsPerComponent = isMask ? PageContentParseXObjects.StencilBits : ImageHeader.Get(
                dictionary,
                KnownName.BitsPerComponent,
                KnownName.BPC,
                true).AsInt32(PageContentParseXObjects.DefaultBits),

            Filters = PageContentParseXObjects.ReadFilters(ImageHeader.Get(dictionary, KnownName.Filter, KnownName.F, true)),
            ColorSpace = ImageHeader.Get(dictionary, KnownName.ColorSpace, KnownName.CS, true),
        };
        PageContentParseObjects.Add(state, item, TextGeometry.TransformRect(state.State.Ctm, PageContentParseXObjects.UnitSquare), new(state.OperatorStart, state.OperatorEnd));
    }

    /// <summary>Adds a form XObject.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "form">The form stream.</param>
    /// <param name = "name">Its resource name.</param>
    internal static void AddForm(PageContentParseState state, PdfStream form, PdfName name)
    {
        var dictionary = form.Dictionary;
        var matrix = PageContentParseXObjects.ReadMatrix(dictionary);
        PdfRectangle? box = dictionary.TryGetRectangle(KnownName.BBox, out var bbox) ? bbox : null;
        var bounds = box is { } rectangle ? TextGeometry.TransformRect(matrix * state.State.Ctm, rectangle) : default;
        var item = new PdfFormObject { Name = name, Stream = form, FormMatrix = matrix, BoundingBox = box, };
        PageContentParseObjects.Add(state, item, bounds, new(state.OperatorStart, state.OperatorEnd));
    }
}
