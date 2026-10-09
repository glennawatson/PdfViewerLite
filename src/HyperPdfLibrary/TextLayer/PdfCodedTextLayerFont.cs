// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.TextLayer;

/// <summary>
/// A composite font written with two-byte codes, such as one made by <c>PdfEmbeddedFonts</c>. Each character has a
/// code; the font's <c>/ToUnicode</c> map turns codes back into text.
/// </summary>
[DebuggerDisplay("PdfCodedTextLayerFont: {_codes.Count} characters")]
public sealed class PdfCodedTextLayerFont : IPdfTextLayerFont
{
    /// <summary>The bits a byte is shifted by to make the high byte of a two-byte code.</summary>
    private const int HighByteShift = 8;

    /// <summary>The bytes of a code.</summary>
    private const int CodeBytes = 2;

    /// <summary>The font dictionary or its reference.</summary>
    private readonly PdfValue _resource;

    /// <summary>The code of each character.</summary>
    private readonly Dictionary<int, ushort> _codes;

    /// <summary>The width of each code in thousandths of an em.</summary>
    private readonly float[] _widths;

    /// <summary>The ascent as a share of the font size.</summary>
    private readonly float _ascent;

    /// <summary>The descent as a positive share of the font size.</summary>
    private readonly float _descent;

    /// <summary>Initializes a new instance of the <see cref="PdfCodedTextLayerFont"/> class.</summary>
    /// <param name="resource">The font dictionary or a reference to it.</param>
    /// <param name="codes">The code of each character the font can show.</param>
    /// <param name="widths">The width of each code in thousandths of an em.</param>
    /// <param name="ascent">The ascent as a share of the font size.</param>
    /// <param name="descent">The descent as a positive share of the font size.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public PdfCodedTextLayerFont(PdfValue resource, Dictionary<int, ushort> codes, float[] widths, float ascent, float descent)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(widths);
        _resource = resource;
        _codes = codes;
        _widths = widths;
        _ascent = ascent;
        _descent = descent;
    }

    /// <inheritdoc/>
    public PdfValue Resource => _resource;

    /// <inheritdoc/>
    public float Ascent => _ascent;

    /// <inheritdoc/>
    public float Descent => _descent;

    /// <inheritdoc/>
    public int Encode(ReadOnlySpan<char> text, Span<byte> codes, out PdfTextExtent extent)
    {
        var count = 0;
        var total = 0F;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!_codes.TryGetValue(rune.Value, out var code) || count + CodeBytes > codes.Length)
            {
                continue;
            }

            codes[count] = (byte)(code >> HighByteShift);
            codes[count + 1] = (byte)code;
            count += CodeBytes;
            total += code < _widths.Length ? _widths[code] : 0;
        }

        extent = new(total, 0, total);
        return count;
    }
}
