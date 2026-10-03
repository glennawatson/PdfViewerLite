// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Rendering;

/// <summary>
/// Recolours rendered pages so white paper becomes a softer paper colour and black ink a softer ink colour, with every
/// shade in between mapped linearly per channel. A dark paper with light ink gives a night tone. The remap uses lookup
/// tables built once, so applying a tone allocates nothing.
/// </summary>
[DebuggerDisplay("Paper {Paper:X6} Ink {Ink:X6}")]
public sealed class PageTone : IEquatable<PageTone>
{
    /// <summary>The values per colour channel.</summary>
    private const int ChannelValues = 256;

    /// <summary>The maximum channel value.</summary>
    private const int ChannelMax = 255;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the green table.</summary>
    private const int GreenTable = ChannelValues;

    /// <summary>The offset of the red table.</summary>
    private const int RedTable = GreenTable + ChannelValues;

    /// <summary>The size of all three tables.</summary>
    private const int TableSize = RedTable + ChannelValues;

    /// <summary>The offset of green within a pixel.</summary>
    private const int GreenByte = 1;

    /// <summary>The offset of red within a pixel.</summary>
    private const int RedByte = 2;

    /// <summary>Half the maximum channel value, used for rounding.</summary>
    private const int Rounding = ChannelMax / RedByte;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of the colour bits.</summary>
    private const uint ColorMask = 0xFFFFFFU;

    /// <summary>The FNV prime used to derive identifiers.</summary>
    private const uint IdPrime = 16_777_619U;

    /// <summary>The blue, green and red lookup tables, in that order.</summary>
    private readonly byte[] _table;

    /// <summary>Initializes a new instance of the <see cref="PageTone"/> class.</summary>
    /// <param name="paper">The colour white paper becomes, as 0xRRGGBB.</param>
    /// <param name="ink">The colour black ink becomes, as 0xRRGGBB.</param>
    public PageTone(uint paper, uint ink)
    {
        Paper = paper & ColorMask;
        Ink = ink & ColorMask;
        IsIdentity = Paper == ColorMask && Ink == 0;
        Id = IsIdentity ? 0 : (int)(((Paper * IdPrime) ^ Ink) | 1U);
        _table = new byte[TableSize];
        Fill(0, Paper & ChannelMax, Ink & ChannelMax);
        Fill(GreenTable, (Paper >> GreenShift) & ChannelMax, (Ink >> GreenShift) & ChannelMax);
        Fill(RedTable, (Paper >> RedShift) & ChannelMax, (Ink >> RedShift) & ChannelMax);
    }

    /// <summary>Gets the tone that leaves pages as the document draws them.</summary>
    public static PageTone None { get; } = new(ColorMask, 0);

    /// <summary>Gets the paper colour as 0xRRGGBB.</summary>
    public uint Paper { get; }

    /// <summary>Gets the ink colour as 0xRRGGBB.</summary>
    public uint Ink { get; }

    /// <summary>Gets a stable identifier used to key cached tiles; 0 for <see cref="None"/>.</summary>
    public int Id { get; }

    /// <summary>Gets a value indicating whether the tone leaves pixels unchanged.</summary>
    public bool IsIdentity { get; }

    /// <inheritdoc/>
    public bool Equals(PageTone? other) => other is not null && Paper == other.Paper && Ink == other.Ink;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PageTone);

    /// <inheritdoc/>
    public override int GetHashCode() => Id;

    /// <summary>Recolours an opaque BGRA buffer in place.</summary>
    /// <param name="target">The buffer.</param>
    public void Apply(RenderTarget target)
    {
        if (IsIdentity)
        {
            return;
        }

        var rowBytes = target.Width * BytesPerPixel;
        for (var y = 0; y < target.Height; y++)
        {
            Apply(target.Pixels.Slice(y * target.Stride, rowBytes));
        }
    }

    /// <summary>Recolours opaque BGRA pixels in place.</summary>
    /// <param name="pixels">The pixel bytes, four per pixel.</param>
    public void Apply(Span<byte> pixels)
    {
        if (IsIdentity)
        {
            return;
        }

        ReadOnlySpan<byte> blue = _table.AsSpan(0, ChannelValues);
        ReadOnlySpan<byte> green = _table.AsSpan(GreenTable, ChannelValues);
        ReadOnlySpan<byte> red = _table.AsSpan(RedTable, ChannelValues);
        for (var i = 0; i + BytesPerPixel <= pixels.Length; i += BytesPerPixel)
        {
            pixels[i] = blue[pixels[i]];
            pixels[i + GreenByte] = green[pixels[i + GreenByte]];
            pixels[i + RedByte] = red[pixels[i + RedByte]];
        }
    }

    /// <summary>Fills one channel's table with a linear ramp from ink (0) to paper (255).</summary>
    /// <param name="offset">The table offset.</param>
    /// <param name="paper">The paper channel value.</param>
    /// <param name="ink">The ink channel value.</param>
    private void Fill(int offset, uint paper, uint ink)
    {
        var span = _table.AsSpan(offset, ChannelValues);
        for (var value = 0; value < ChannelValues; value++)
        {
            span[value] = (byte)((((int)ink * (ChannelMax - value)) + ((int)paper * value) + Rounding) / ChannelMax);
        }
    }
}
