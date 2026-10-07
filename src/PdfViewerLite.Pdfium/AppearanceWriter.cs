// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Writes PDF content stream or dictionary text into a pooled buffer, then hands it to PDFium as an annotation's
/// appearance or as a string value. Callers hold the PDFium lock and call <see cref="Dispose"/> in a <c>finally</c>
/// block (a <c>using</c> declaration would make a read-only copy and lose what was written).
/// </summary>
internal unsafe ref struct AppearanceWriter
{
    /// <summary>The characters a formatted number can take, with room to spare.</summary>
    private const int NumberChars = 32;

    /// <summary>The largest channel value.</summary>
    private const double ChannelMax = 255;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>One channel's bits.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The normal appearance mode.</summary>
    private const int AppearanceNormal = 0;

    /// <summary>How much the buffer grows when it is full.</summary>
    private const int Growth = 2;

    /// <summary>The pooled buffer.</summary>
    private char[] _buffer;

    /// <summary>The characters written.</summary>
    private int _length;

    /// <summary>Initializes a new instance of the <see cref="AppearanceWriter"/> struct.</summary>
    /// <param name="capacity">The characters expected.</param>
    public AppearanceWriter(int capacity) => _buffer = ArrayPool<char>.Shared.Rent(capacity);

    /// <summary>Gets the text written so far.</summary>
    public readonly ReadOnlySpan<char> Written => _buffer.AsSpan(0, _length);

    /// <summary>Appends text.</summary>
    /// <param name="text">The text.</param>
    internal void Append(scoped ReadOnlySpan<char> text)
    {
        Ensure(text.Length);
        text.CopyTo(_buffer.AsSpan(_length));
        _length += text.Length;
    }

    /// <summary>Appends a number with at most three decimals, followed by a space.</summary>
    /// <param name="value">The number.</param>
    internal void Number(double value)
    {
        Ensure(NumberChars);
        _ = value.TryFormat(_buffer.AsSpan(_length), out var written, "0.###", CultureInfo.InvariantCulture);
        _length += written;
        _buffer[_length] = ' ';
        _length++;
    }

    /// <summary>Appends a point and an operator, such as <c>m</c> or <c>l</c>, ending the line.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="op">The operator.</param>
    internal void Point(double x, double y, scoped ReadOnlySpan<char> op)
    {
        Number(x);
        Number(y);
        Append(op);
        Append("\n");
    }

    /// <summary>Appends a colour as three numbers from 0 to 1, followed by an operator such as <c>RG</c>.</summary>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="op">The operator.</param>
    internal void Color(uint color, scoped ReadOnlySpan<char> op)
    {
        Number(((color >> RedShift) & ChannelMask) / ChannelMax);
        Number(((color >> GreenShift) & ChannelMask) / ChannelMax);
        Number((color & ChannelMask) / ChannelMax);
        Append(op);
    }

    /// <summary>Sets what was written as the annotation's normal appearance, sized to its rectangle.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when set.</returns>
    internal bool ApplyAppearance(nint annotation)
    {
        Terminate();
        fixed (char* text = _buffer)
        {
            return NativeMethods.FPDFAnnot_SetAP(annotation, AppearanceNormal, text) != 0;
        }
    }

    /// <summary>Sets what was written as a string value of the annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The null terminated key.</param>
    /// <returns><see langword="true"/> when set.</returns>
    internal bool ApplyString(nint annotation, ReadOnlySpan<byte> key)
    {
        Terminate();
        fixed (byte* keyPointer = key)
        {
            fixed (char* text = _buffer)
            {
                return NativeMethods.FPDFAnnot_SetStringValue(annotation, keyPointer, text) != 0;
            }
        }
    }

    /// <summary>Returns the buffer to the pool.</summary>
    internal void Dispose()
    {
        if (_buffer.Length == 0)
        {
            return;
        }

        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = [];
    }

    /// <summary>Ends the text with a null character, which PDFium expects.</summary>
    private void Terminate()
    {
        Ensure(1);
        _buffer[_length] = '\0';
    }

    /// <summary>Grows the buffer to fit more characters.</summary>
    /// <param name="more">The characters about to be written.</param>
    private void Ensure(int more)
    {
        if (_length + more <= _buffer.Length)
        {
            return;
        }

        var larger = ArrayPool<char>.Shared.Rent(Math.Max(_buffer.Length * Growth, _length + more));
        _buffer.AsSpan(0, _length).CopyTo(larger);
        ArrayPool<char>.Shared.Return(_buffer);
        _buffer = larger;
    }
}
