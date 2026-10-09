// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// Reads a content stream into page objects. It follows the graphics and text state with its own small tracker, so the
/// interpreter that draws pages is untouched, and it records the bytes each object came from so unchanged objects can be
/// written back as they were. One parser reads one stream.
/// </summary>
[DebuggerDisplay("PageContentParser: {_objects.Count} objects")]
internal sealed partial class PageContentParser
{
    /// <summary>The initial capacity of the object list.</summary>
    private const int ObjectCapacity = 64;

    /// <summary>The initial capacity of the state stack.</summary>
    private const int StackCapacity = 16;

    /// <summary>The initial capacity of a path's segment list.</summary>
    private const int SegmentCapacity = 32;

    /// <summary>How many operators are read between checks of the cancellation token.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>The operand index of the third operand.</summary>
    private const int ThirdOperand = 2;

    /// <summary>The operand index of the fourth operand.</summary>
    private const int FourthOperand = 3;

    /// <summary>The operand index of the fifth operand.</summary>
    private const int FifthOperand = 4;

    /// <summary>The operand index of the sixth operand.</summary>
    private const int SixthOperand = 5;

    /// <summary>The page's caches.</summary>
    private readonly PdfRenderCache _cache;

    /// <summary>The name table operands are interned in.</summary>
    private readonly PdfNameTable _names;

    /// <summary>The content the objects belong to.</summary>
    private readonly PdfPageContent _owner;

    /// <summary>The decoded content.</summary>
    private readonly byte[] _content;

    /// <summary>The resources named in the content.</summary>
    private readonly PdfDictionary? _resources;

    /// <summary>The objects found, in painting order.</summary>
    private readonly List<PdfPageObject> _objects = [with(ObjectCapacity)];

    /// <summary>The saved states.</summary>
    private readonly List<ParseState> _stack = [with(StackCapacity)];

    /// <summary>The open marked-content sequences.</summary>
    private readonly List<PdfMark> _marks = [];

    /// <summary>The segments of the path being built.</summary>
    private readonly List<PdfPathSegment> _segments = [with(SegmentCapacity)];

    /// <summary>The current state.</summary>
    private ParseState _state;

    /// <summary>The marks as an array, made when an object needs them and dropped when they change.</summary>
    private PdfMark[]? _markSnapshot;

    /// <summary>The offset where the current operator and its operands start.</summary>
    private int _operatorStart;

    /// <summary>The offset after the current operator.</summary>
    private int _operatorEnd;

    /// <summary>Restores that had no state to restore.</summary>
    private int _underflow;

    /// <summary>Initializes a new instance of the <see cref="PageContentParser"/> class.</summary>
    /// <param name="owner">The content the objects belong to.</param>
    /// <param name="content">The decoded content.</param>
    /// <param name="resources">The resources the content names, or <see langword="null"/>.</param>
    /// <param name="start">The matrix from the content's space to user space.</param>
    /// <param name="clip">The clip the content starts inside, or <see langword="null"/>.</param>
    internal PageContentParser(PdfPageContent owner, byte[] content, PdfDictionary? resources, Matrix3x2 start, ClipNode? clip)
    {
        _owner = owner;
        _cache = HyperPdfLibrary.Document.PdfDocumentRendering.GetRenderCache(owner.Document);
        _names = owner.Document.Objects.Names;
        _content = content;
        _resources = resources;
        _state = ParseState.Start(start);
        _state.Clip = clip;
    }

    /// <summary>Gets the number of restores that had no matching save.</summary>
    internal int Underflow => _underflow;

    /// <summary>Gets the number of saves left open at the end.</summary>
    internal int Unclosed => _stack.Count;

    /// <summary>Reads the whole content.</summary>
    /// <param name="cancellationToken">Checked every <see cref="CancellationInterval"/> operators.</param>
    /// <returns>The objects in painting order.</returns>
    internal List<PdfPageObject> Parse(CancellationToken cancellationToken)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(_content, _names, operands);
        var previousEnd = 0;
        var count = 0;
        while (reader.Next(out var op))
        {
            count++;
            if (count % CancellationInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            _operatorStart = previousEnd;
            _operatorEnd = reader.Position;
            previousEnd = _operatorEnd;
            Dispatch(op, ref reader);
        }

        return _objects;
    }

    /// <summary>Determines whether an exception from a damaged object should only skip the operator that hit it.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the exception is recoverable.</returns>
    private static bool IsRecoverable(Exception exception) =>
        exception is InvalidDataException or PdfException or ArgumentException or InvalidOperationException or IndexOutOfRangeException
            or NotSupportedException or FormatException or OverflowException;

    /// <summary>Finds a named resource.</summary>
    /// <param name="category">The resource category, such as /Font.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The value, resolved; null when missing.</returns>
    private PdfValue FindResource(KnownName category, PdfName name) => _resources?.GetDictionary(category)?.Get(name) ?? default;

    /// <summary>Gets the open marked-content sequences as an array.</summary>
    /// <returns>The marks, outermost first.</returns>
    private PdfMark[] CurrentMarks() => _markSnapshot ??= [.. _marks];

    /// <summary>Records an object with the state it was painted in.</summary>
    /// <param name="item">The object.</param>
    /// <param name="bounds">The object's bounds in user space.</param>
    /// <param name="source">The bytes it came from.</param>
    private void Add(PdfPageObject item, in PdfRectangle bounds, ByteRange source)
    {
        item.Owner = _owner;
        item.Source = source;
        item.Matrix = _state.Ctm;
        item.OriginalBounds = bounds;
        item.ClipChain = _state.Clip;
        item.ClipBounds = _state.Clip?.Bounds ?? PdfPageObject.Unbounded;
        item.Marks = CurrentMarks();
        item.LineWidth = _state.LineWidth;
        item.OriginalFillPaint = _state.Fill;
        item.OriginalStrokePaint = _state.Stroke;
        item.Index = _objects.Count;
        _objects.Add(item);
    }
}
