// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Raster;

/// <summary>
/// Reads a page's content operators, and those of the forms it paints, to list its images and say whether it paints
/// anything else. It tracks only the matrix and the text render mode, and never decodes an image or a font.
/// </summary>
internal sealed class RasterPageScanner
{
    /// <summary>The render mode that neither fills nor strokes.</summary>
    private const int InvisibleMode = 3;

    /// <summary>The most nested <c>q</c> operators whose state is saved; deeper ones are counted but not restored.</summary>
    private const int StackDepth = 64;

    /// <summary>The numbers in a matrix.</summary>
    private const int MatrixNumbers = 6;

    /// <summary>The index of a matrix's third number.</summary>
    private const int MatrixC = 2;

    /// <summary>The index of a matrix's fourth number.</summary>
    private const int MatrixD = 3;

    /// <summary>The index of a matrix's fifth number.</summary>
    private const int MatrixE = 4;

    /// <summary>The index of a matrix's sixth number.</summary>
    private const int MatrixF = 5;

    /// <summary>The newline written between the streams of a content array.</summary>
    private const byte StreamSeparator = 0x0A;

    /// <summary>The bytes of the <c>&lt;&lt;</c> and <c>&gt;&gt;</c> that wrap an inline image's entries.</summary>
    private const int DictionaryDelimiters = 4;

    /// <summary>The width of one delimiter pair.</summary>
    private const int DelimiterWidth = 2;

    /// <summary>The saved matrices.</summary>
    private readonly Matrix3x2[] _matrices = new Matrix3x2[StackDepth];

    /// <summary>The saved render modes.</summary>
    private readonly int[] _modes = new int[StackDepth];

    /// <summary>The resource dictionaries in force, innermost last.</summary>
    private readonly List<PdfDictionary?> _resources = [];

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The current matrix.</summary>
    private Matrix3x2 _ctm;

    /// <summary>The current text render mode.</summary>
    private int _mode;

    /// <summary>The number of open <c>q</c> operators.</summary>
    private int _depth;

    /// <summary>Initializes a new instance of the <see cref="RasterPageScanner"/> class.</summary>
    /// <param name="document">The document.</param>
    internal RasterPageScanner(PdfDocument document) => _document = document;

    /// <summary>Gets the images found, in painting order.</summary>
    internal List<PdfRasterImage> Images { get; } = [];

    /// <summary>Gets a value indicating whether a path or shading was painted.</summary>
    internal bool HasVectorContent { get; private set; }

    /// <summary>Gets a value indicating whether visible text was shown.</summary>
    internal bool HasVisibleText { get; private set; }

    /// <summary>Gets a value indicating whether invisible text was shown.</summary>
    internal bool HasOcrText { get; private set; }

    /// <summary>Scans a page's content.</summary>
    /// <param name="page">The page.</param>
    internal void Scan(PdfPage page)
    {
        Images.Clear();
        _resources.Clear();
        HasVectorContent = false;
        HasVisibleText = false;
        HasOcrText = false;
        _ctm = Matrix3x2.Identity;
        _mode = 0;
        _depth = 0;
        _resources.Add(page.Resources);
        var content = default(PooledBuffer);
        try
        {
            AppendContents(page.Dictionary.Get(KnownName.Contents), ref content);
            ScanStream(content.WrittenSpan, 0);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Decodes a page's <c>/Contents</c>, a stream or an array of streams, into one buffer.</summary>
    /// <param name="contents">The <c>/Contents</c> value.</param>
    /// <param name="buffer">Receives the content.</param>
    private static void AppendContents(PdfValue contents, ref PooledBuffer buffer)
    {
        if (contents.AsStream() is { } single)
        {
            _ = single.Decode(ref buffer);
            return;
        }

        var array = contents.AsArray();
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (array.Get(i).AsStream() is not { } part)
            {
                continue;
            }

            var piece = default(PooledBuffer);
            try
            {
                _ = part.Decode(ref piece);
                buffer.Write(piece.WrittenSpan);
                buffer.WriteByte(StreamSeparator);
            }
            finally
            {
                piece.Dispose();
            }
        }
    }

    /// <summary>Reads a matrix entry.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The /Matrix; identity when missing or short.</returns>
    private static Matrix3x2 ReadFormMatrix(PdfDictionary dictionary)
    {
        Span<float> numbers = stackalloc float[MatrixNumbers];
        return dictionary.GetArray(KnownName.Matrix) is { Count: >= MatrixNumbers } array && array.ReadNumbers(numbers) >= MatrixNumbers
            ? new(numbers[0], numbers[1], numbers[MatrixC], numbers[MatrixD], numbers[MatrixE], numbers[MatrixF])
            : Matrix3x2.Identity;
    }

    /// <summary>Scans one content stream.</summary>
    /// <param name="content">The decoded content.</param>
    /// <param name="formDepth">The form nesting depth.</param>
    private void ScanStream(ReadOnlySpan<byte> content, int formDepth)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, _document.Objects.Names, operands);
        var floor = _depth;
        while (reader.Next(out var op))
        {
            Apply(RasterOperatorClasses.Classify(op), ref reader, floor, formDepth);
        }

        _depth = floor;
    }

    /// <summary>Applies one operator.</summary>
    /// <param name="kind">The operator's class.</param>
    /// <param name="reader">The reader, positioned on the operator.</param>
    /// <param name="floor">The save depth the stream started at; <c>Q</c> never pops below it.</param>
    /// <param name="formDepth">The form nesting depth.</param>
    private void Apply(RasterOperatorClass kind, ref ContentReader reader, int floor, int formDepth)
    {
        switch (kind)
        {
            case RasterOperatorClass.Save:
            {
                Save();
                break;
            }

            case RasterOperatorClass.Restore:
            {
                Restore(floor);
                break;
            }

            case RasterOperatorClass.Matrix:
            {
                _ctm = new Matrix3x2(reader.Number(0), reader.Number(1), reader.Number(MatrixC), reader.Number(MatrixD), reader.Number(MatrixE), reader.Number(MatrixF)) * _ctm;
                break;
            }

            case RasterOperatorClass.PaintXObject:
            {
                PaintXObject(reader.Operand(0).Name, formDepth);
                break;
            }

            default:
            {
                ApplyOther(kind, ref reader);
                break;
            }
        }
    }

    /// <summary>Applies the operators that do not touch the matrix stack.</summary>
    /// <param name="kind">The operator's class.</param>
    /// <param name="reader">The reader, positioned on the operator.</param>
    private void ApplyOther(RasterOperatorClass kind, ref ContentReader reader)
    {
        switch (kind)
        {
            case RasterOperatorClass.InlineImage:
            {
                AddInlineImage(ref reader);
                break;
            }

            case RasterOperatorClass.Vector:
            {
                HasVectorContent = true;
                break;
            }

            case RasterOperatorClass.ShowText:
            {
                HasOcrText |= _mode == InvisibleMode;
                HasVisibleText |= _mode != InvisibleMode;
                break;
            }

            case RasterOperatorClass.RenderMode:
            {
                _mode = (int)reader.Number(0);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Handles <c>q</c>.</summary>
    private void Save()
    {
        if (_depth < StackDepth)
        {
            _matrices[_depth] = _ctm;
            _modes[_depth] = _mode;
        }

        _depth++;
    }

    /// <summary>Handles <c>Q</c>.</summary>
    /// <param name="floor">The save depth the stream started at.</param>
    private void Restore(int floor)
    {
        if (_depth <= floor)
        {
            return;
        }

        _depth--;
        if (_depth >= StackDepth)
        {
            return;
        }

        _ctm = _matrices[_depth];
        _mode = _modes[_depth];
    }

    /// <summary>Handles <c>Do</c>.</summary>
    /// <param name="name">The XObject's resource name.</param>
    /// <param name="formDepth">The form nesting depth.</param>
    private void PaintXObject(PdfName name, int formDepth)
    {
        if (formDepth >= PdfLimits.MaxDrawDepth || FindResource(KnownName.XObject, name).AsStream() is not { } xobject)
        {
            return;
        }

        var dictionary = xobject.Dictionary;
        var membership = dictionary.GetRaw(KnownName.OC);
        if (!membership.IsNull && !_document.OptionalContent.IsVisible(membership))
        {
            return;
        }

        if (dictionary.IsName(KnownName.Subtype, KnownName.Form))
        {
            ScanForm(xobject, formDepth);
            return;
        }

        if (dictionary.IsName(KnownName.Subtype, KnownName.Image))
        {
            Images.Add(RasterImageReader.Describe(_document.Objects.Names, dictionary, _ctm, false, CurrentColorSpaces()));
        }
    }

    /// <summary>Scans a form XObject with its matrix and resources.</summary>
    /// <param name="form">The form.</param>
    /// <param name="formDepth">The form nesting depth.</param>
    private void ScanForm(PdfStream form, int formDepth)
    {
        var ctm = _ctm;
        var mode = _mode;
        _ctm = ReadFormMatrix(form.Dictionary) * _ctm;
        _resources.Add(form.Dictionary.GetDictionary(KnownName.Resources) ?? _resources[^1]);
        var content = default(PooledBuffer);
        try
        {
            _ = form.Decode(ref content);
            ScanStream(content.WrittenSpan, formDepth + 1);
        }
        finally
        {
            content.Dispose();
            _resources.RemoveAt(_resources.Count - 1);
            _ctm = ctm;
            _mode = mode;
        }
    }

    /// <summary>Records the inline image the reader has just found.</summary>
    /// <param name="reader">The reader.</param>
    private void AddInlineImage(ref ContentReader reader)
    {
        var content = reader.Content;
        var range = reader.InlineImageDictionary;
        var offset = range.Start.GetOffset(content.Length);
        var length = range.End.GetOffset(content.Length) - offset;
        var text = new byte[length + DictionaryDelimiters];
        "<<"u8.CopyTo(text);
        content.Slice(offset, length).CopyTo(text.AsSpan(DelimiterWidth));
        ">>"u8.CopyTo(text.AsSpan(length + DelimiterWidth));
        var parser = new PdfParser(text, 0, _document.Objects, _document.Objects.Names);
        if (parser.ParseValue().AsDictionary() is { } dictionary)
        {
            Images.Add(RasterImageReader.Describe(_document.Objects.Names, dictionary, _ctm, true, CurrentColorSpaces()));
        }
    }

    /// <summary>Gets the /ColorSpace resources in force.</summary>
    /// <returns>The dictionary, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfDictionary? CurrentColorSpaces() => _resources[^1]?.GetDictionary(KnownName.ColorSpace);

    /// <summary>Finds a named resource, innermost resources first.</summary>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The resource; null when missing.</returns>
    private PdfValue FindResource(KnownName category, PdfName name)
    {
        for (var i = _resources.Count - 1; i >= 0; i--)
        {
            var value = _resources[i]?.GetDictionary(category)?.Get(name) ?? default;
            if (!value.IsNull)
            {
                return value;
            }
        }

        return default;
    }
}
