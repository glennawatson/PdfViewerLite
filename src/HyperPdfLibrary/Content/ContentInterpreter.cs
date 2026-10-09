// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Runs content streams and reports what they draw to an <see cref="IContentDevice"/>. Operators are dispatched through a
/// table of function pointers indexed by <see cref="ContentOperator"/>. One interpreter serves one thread; it reuses its
/// state stack and path, so running a stream allocates only what the streams' objects need.
/// </summary>
[DebuggerDisplay("ContentInterpreter: depth {_depth}, stack {_stack.Count}")]
internal sealed partial class ContentInterpreter : IDisposable
{
    /// <summary>The initial capacity of the state stack.</summary>
    private const int StackCapacity = 16;

    /// <summary>The operators run between checks of the running call's cancellation token.</summary>
    private const int CancelCheckOperators = 128;

    /// <summary>The newline written between the streams of a content array.</summary>
    private const byte StreamSeparator = (byte)'\n';

    /// <summary>The document's caches.</summary>
    private readonly PdfRenderCache _cache;

    /// <summary>The device everything is drawn on.</summary>
    private readonly IContentDevice _device;

    /// <summary>The device as a text collector told where each text-showing operator starts; null for other devices.</summary>
    private readonly ITextObjectDevice? _textObjects;

    /// <summary>The document's layers.</summary>
    private readonly PdfOptionalContent _layers;

    /// <summary>The saved graphics states.</summary>
    private readonly List<GraphicsState> _stack = [with(StackCapacity)];

    /// <summary>The resource dictionaries of the streams being run, innermost last.</summary>
    private readonly List<PdfDictionary?> _resources = [];

    /// <summary>Whether each open marked content level hides its content, innermost last.</summary>
    private readonly List<bool> _marked = [];

    /// <summary>The current path, reused for every path.</summary>
    private readonly SKPathBuilder _path = new();

    /// <summary>The glyph outlines collected for a text clip.</summary>
    private readonly SKPathBuilder _textClip = new();

    /// <summary>The current graphics state.</summary>
    private GraphicsState _state = new();

    /// <summary>The current point of the path.</summary>
    private SKPoint _current;

    /// <summary>The segments added to the current path; zero means the path is empty.</summary>
    private int _pathPoints;

    /// <summary>The glyph outlines added to the text clip since it was last applied.</summary>
    private int _textClipGlyphs;

    /// <summary>The text matrix.</summary>
    private Matrix3x2 _tm = Matrix3x2.Identity;

    /// <summary>The text line matrix.</summary>
    private Matrix3x2 _tlm = Matrix3x2.Identity;

    /// <summary>The matrix from the current stream's default space to the page, which patterns are placed against.</summary>
    private Matrix3x2 _patternBase = Matrix3x2.Identity;

    /// <summary>The nesting of forms, patterns and Type 3 glyphs being run.</summary>
    private int _depth;

    /// <summary>The state stack depth the current stream may not restore below.</summary>
    private int _stackFloor;

    /// <summary>The marked content depth the current stream may not close below.</summary>
    private int _markedFloor;

    /// <summary>The number of open marked content levels that hide their content.</summary>
    private int _hidden;

    /// <summary>Whether the next painting operator applies a clip.</summary>
    private bool _pendingClip;

    /// <summary>Whether the pending clip uses the even-odd rule.</summary>
    private bool _pendingClipEvenOdd;

    /// <summary>Whether colour operators are ignored, as inside uncoloured patterns and Type 3 glyphs that give only a shape.</summary>
    private bool _colorLocked;

    /// <summary>Initializes a new instance of the <see cref="ContentInterpreter"/> class.</summary>
    /// <param name="cache">The document's caches.</param>
    /// <param name="device">The device to draw on.</param>
    /// <param name="depth">The nesting depth already in use, for streams run from inside others.</param>
    internal ContentInterpreter(PdfRenderCache cache, IContentDevice device, int depth)
    {
        _cache = cache;
        _device = device;
        _textObjects = device as ITextObjectDevice;
        _layers = PdfDocumentLayers.GetOptionalContent(cache.Document);
        _depth = depth;
    }

    /// <summary>Gets or sets a value indicating whether optional content follows print usage rather than the viewer's layer switches.</summary>
    internal bool Printing { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _path.Dispose();
        _textClip.Dispose();
    }

    /// <summary>Decodes a page's /Contents, a stream or an array of streams, into one buffer.</summary>
    /// <param name="page">The page.</param>
    /// <param name="buffer">Receives the content.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeContents(PdfPage page, ref PooledBuffer buffer) => AppendContents(page.Dictionary.Get(KnownName.Contents), ref buffer);

    /// <summary>Decodes a /Contents value, a stream or an array of streams, into one buffer.</summary>
    /// <param name="contents">The /Contents value.</param>
    /// <param name="buffer">Receives the content.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeContents(PdfValue contents, ref PooledBuffer buffer) => AppendContents(contents, ref buffer);

    /// <summary>Runs a page's content, then returns the device to its starting state.</summary>
    /// <param name="page">The page; content is drawn in viewer space.</param>
    internal void RunPage(PdfPage page)
    {
        BeginPage(page);
        var content = default(PooledBuffer);
        try
        {
            DecodeContents(page, ref content);
            Execute(content.WrittenSpan);
        }
        finally
        {
            content.Dispose();
        }

        EndPage();
    }

    /// <summary>Starts running a page's content in slices with <see cref="RunSlice"/>; content is drawn in viewer space.</summary>
    /// <param name="page">The page.</param>
    internal void BeginPage(PdfPage page)
    {
        _state = new() { Ctm = page.ViewerTransform };
        _patternBase = page.ViewerTransform;
        _resources.Add(page.Resources);
    }

    /// <summary>
    /// Runs up to a number of top-level operators of the page's content, then stops at an operator boundary so the
    /// caller can pause. Forms, patterns and glyphs an operator draws always run to their end.
    /// </summary>
    /// <param name="content">The page's whole decoded content.</param>
    /// <param name="position">The offset to resume at; receives the offset to resume from next time.</param>
    /// <param name="operators">The most operators to run.</param>
    /// <returns><see langword="true"/> when the content has ended.</returns>
    internal bool RunSlice(ReadOnlySpan<byte> content, ref int position, int operators)
    {
        PdfCancellation.ThrowIfCancelled();
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, _cache.Names, operands) { Position = position };
        for (var i = 0; i < operators; i++)
        {
            if (!reader.Next(out var op))
            {
                position = content.Length;
                return true;
            }

            Dispatch(op, ref reader);
        }

        position = reader.Position;
        return false;
    }

    /// <summary>Ends a page run in slices, returning the device to its starting state.</summary>
    internal void EndPage()
    {
        Unwind();
        _resources.Clear();
    }

    /// <summary>Draws a form XObject with a transform, as annotation appearances are drawn.</summary>
    /// <param name="form">The form stream.</param>
    /// <param name="ctm">The matrix from the form's space to the page.</param>
    /// <param name="inherited">The resources used when the form has none of its own, or null.</param>
    internal void RunForm(PdfStream form, Matrix3x2 ctm, PdfDictionary? inherited)
    {
        _state = new() { Ctm = ctm };
        _patternBase = ctm;
        _resources.Add(inherited);
        DrawForm(form);
        _resources.Clear();
    }

    /// <summary>Runs a tiling pattern's content in pattern space.</summary>
    /// <param name="pattern">The pattern stream.</param>
    /// <param name="uncolored">Whether the pattern takes its colour from the colour that selected it.</param>
    /// <param name="rgb">That colour as 0xRRGGBB.</param>
    internal void RunPattern(PdfStream pattern, bool uncolored, uint rgb)
    {
        _state = new() { Ctm = Matrix3x2.Identity };
        _patternBase = Matrix3x2.Identity;
        if (uncolored)
        {
            var colour = new ColorState(PdfColorSpace.DeviceRgb, rgb, null, false);
            _state.Fill = colour;
            _state.Stroke = colour;
            _colorLocked = true;
        }

        _resources.Add(pattern.Dictionary.GetDictionary(KnownName.Resources));
        RunStreamBody(pattern);
        Unwind();
        _resources.Clear();
    }

    /// <summary>Determines whether an exception from a damaged object should only skip the operator that hit it.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the exception is recoverable.</returns>
    private static bool IsRecoverable(Exception exception) =>
        exception is InvalidDataException or PdfException or ArgumentException or InvalidOperationException or IndexOutOfRangeException
            or NotSupportedException or FormatException or OverflowException;

    /// <summary>Decodes a page's /Contents, a stream or an array of streams, into one buffer.</summary>
    /// <param name="contents">The /Contents value.</param>
    /// <param name="buffer">Receives the content.</param>
    private static void AppendContents(PdfValue contents, ref PooledBuffer buffer)
    {
        if (contents.AsStream() is { } single)
        {
            _ = single.Decode(ref buffer);
            return;
        }

        if (contents.AsArray() is not { } array)
        {
            return;
        }

        for (var i = 0; i < array.Count; i++)
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

    /// <summary>Runs a stream's content with the current state and resources.</summary>
    /// <param name="stream">The stream.</param>
    private void RunStreamBody(PdfStream stream)
    {
        var content = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref content);
            Execute(content.WrittenSpan);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Runs content, dispatching each operator through the table.</summary>
    /// <param name="content">The decoded content.</param>
    private void Execute(ReadOnlySpan<byte> content)
    {
        var stackFloor = _stackFloor;
        var markedFloor = _markedFloor;
        _stackFloor = _stack.Count;
        _markedFloor = _marked.Count;
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, _cache.Names, operands);
        var batch = 0;
        while (reader.Next(out var op))
        {
            Dispatch(op, ref reader);
            batch = (batch + 1) % CancelCheckOperators;
            if (batch == 0)
            {
                PdfCancellation.ThrowIfCancelled();
            }
        }

        Unwind();
        _stackFloor = stackFloor;
        _markedFloor = markedFloor;
    }

    /// <summary>Restores every state saved by the current stream and closes its marked content.</summary>
    private void Unwind()
    {
        while (_stack.Count > _stackFloor)
        {
            RestoreState();
        }

        while (_marked.Count > _markedFloor)
        {
            EndMarked();
        }

        _pendingClip = false;
        _path.Reset();
        _pathPoints = 0;
    }

    /// <summary>Gets the innermost resource dictionary.</summary>
    /// <returns>The dictionary, or null.</returns>
    private PdfDictionary? CurrentResources() => _resources.Count == 0 ? null : _resources[^1];

    /// <summary>Finds a named resource, searching the innermost resources first.</summary>
    /// <param name="category">The resource category, such as /Font.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The value, resolved; null when missing.</returns>
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
