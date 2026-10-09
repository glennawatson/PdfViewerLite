// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// An Indexed colour space: a palette of base-space colours. The palette is converted to BGRA once, so a row converts by
/// table lookup. Indexes above /HiVal are black, as in PDFium.
/// </summary>
internal sealed class IndexedColorSpace : PdfColorSpace
{
    /// <summary>The largest palette index.</summary>
    internal const int MaxHighValue = 255;

    /// <summary>The base colour space.</summary>
    private readonly PdfColorSpace _base;

    /// <summary>The palette: base-space sample bytes per entry.</summary>
    private readonly byte[] _palette;

    /// <summary>The base space's 8-bit decode ranges, which the palette bytes scale to.</summary>
    private readonly float[] _baseDecode;

    /// <summary>The largest valid index.</summary>
    private readonly int _highValue;

    /// <summary>The BGRA word for every byte index.</summary>
    private readonly uint[] _lookup;

    /// <summary>Initializes a new instance of the <see cref="IndexedColorSpace"/> class.</summary>
    /// <param name="baseSpace">The base colour space.</param>
    /// <param name="highValue">The largest valid index, 0 to 255.</param>
    /// <param name="lookup">The palette bytes; missing bytes read as zero.</param>
    internal IndexedColorSpace(PdfColorSpace baseSpace, int highValue, ReadOnlySpan<byte> lookup)
    {
        _base = baseSpace;
        _baseDecode = baseSpace.GetDefaultDecode(ByteBits);
        _highValue = Math.Clamp(highValue, 0, MaxHighValue);
        var entries = _highValue + 1;
        _palette = new byte[entries * baseSpace.Components];
        lookup[..Math.Min(lookup.Length, _palette.Length)].CopyTo(_palette);
        _lookup = new uint[PixelConverter.LookupSize];
        var bgra = new byte[entries * PixelConverter.BytesPerPixel];
        baseSpace.ConvertRow(_palette, bgra, entries);
        var words = MemoryMarshal.Cast<byte, uint>(bgra);
        Span<float> black = stackalloc float[RgbComponents];
        var blackWord = PixelConverter.PackNative(black);
        for (var i = 0; i < _lookup.Length; i++)
        {
            _lookup[i] = i <= _highValue ? words[i] : blackWord;
        }
    }

    /// <inheritdoc/>
    public override int Components => 1;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.Indexed;

    /// <summary>Gets the base colour space.</summary>
    internal PdfColorSpace Base => _base;

    /// <inheritdoc/>
    public override float[] GetDefaultDecode(int bitsPerComponent) => [0, (1 << Math.Clamp(bitsPerComponent, 1, ByteBits)) - 1];

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb)
    {
        // Like PDFium, an index outside 0..HiVal is black.
        var rounded = float.IsFinite(components[0]) ? MathF.Round(components[0]) : 0;
        if (rounded < 0 || rounded > _highValue)
        {
            rgb[..RgbComponents].Clear();
            return;
        }

        var index = (int)rounded;
        var count = _base.Components;
        var decode = _baseDecode;
        Span<float> values = stackalloc float[MaxComponents];
        values = values[..count];
        for (var i = 0; i < count; i++)
        {
            var low = decode[PairSize * i];
            values[i] = low + (_palette[(index * count) + i] * (decode[(PairSize * i) + 1] - low) / PixelConverter.MaxByte);
        }

        _base.ToRgb(values, rgb);
    }

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.LookupToBgra(samples, bgra, pixelCount, _lookup);
}
