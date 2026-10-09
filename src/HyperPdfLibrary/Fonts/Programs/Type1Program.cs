// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// A Type 1 font program from a PFA or PFB file or a PDF FontFile stream. Charstrings are decrypted once when the font
/// is parsed; decoding a glyph runs the Type 1 charstring interpreter without allocating.
/// </summary>
[DebuggerDisplay("Type1Program: {GlyphCount} glyphs")]
public sealed class Type1Program : FontProgram
{
    /// <summary>The parsed font pieces.</summary>
    private readonly Type1Parts _parts;

    /// <summary>The glyph of each code of the built-in encoding, -1 when unmapped.</summary>
    private readonly short[] _encoding;

    /// <summary>Initializes a new instance of the <see cref="Type1Program"/> class.</summary>
    /// <param name="parts">The parsed font pieces.</param>
    private Type1Program(Type1Parts parts)
    {
        _parts = parts;
        _encoding = new short[FontEncodings.CodeCount];
        _encoding.AsSpan().Fill(NotFound);
        if (parts.UsesStandardEncoding)
        {
            FillStandardEncoding();
        }

        foreach (var (code, name) in parts.EncodingEntries)
        {
            _encoding[code] = (short)FindGlyphByName(name.Of(parts.Names));
        }
    }

    /// <inheritdoc/>
    public override int GlyphCount => _parts.Glyphs.Length;

    /// <inheritdoc/>
    public override FontMatrix FontMatrix => _parts.Matrix;

    /// <inheritdoc/>
    public override float UnitsPerEm => _parts.Matrix.A > 0 ? MathF.Round(1 / _parts.Matrix.A) : MathF.Round(1 / FontMatrix.Default.A);

    /// <inheritdoc/>
    public override PdfRectangle BoundingBox => _parts.BoundingBox;

    /// <summary>Parses a Type 1 font, finding the encrypted part by its eexec keyword.</summary>
    /// <param name="data">The font data: PFA, PFB or a FontFile stream.</param>
    /// <param name="program">The program.</param>
    /// <returns><see langword="true"/> when the font has charstrings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out Type1Program? program) => TryParse(data, 0, out program);

    /// <summary>Parses a Type 1 font from a PDF FontFile stream.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <param name="length1">The stream's /Length1: the length of the cleartext part. It is checked against the data, since it is often wrong.</param>
    /// <param name="program">The program.</param>
    /// <returns><see langword="true"/> when the font has charstrings.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, int length1, [NotNullWhen(true)] out Type1Program? program)
    {
        program = Type1Parser.TryParse(data, length1, out var parts) && parts is not null ? new Type1Program(parts) : null;
        return program is not null;
    }

    /// <inheritdoc/>
    public override void DecodeGlyph<TSink>(int glyph, ref TSink sink)
    {
        var outcome = RunCharstring(glyph, default, false, ref sink);
        if (!outcome.HasSeac)
        {
            return;
        }

        _ = RunCharstring(GetStandardGlyph(outcome.BaseCode), default, false, ref sink);
        _ = RunCharstring(GetStandardGlyph(outcome.AccentCode), new(outcome.AccentX, outcome.AccentY), false, ref sink);
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> GetGlyphName(int glyph) =>
        (uint)glyph < (uint)_parts.GlyphNames.Length ? _parts.GlyphNames[glyph].Of(_parts.Names) : [];

    /// <inheritdoc/>
    public override int GetGlyphByCharCode(int code) => (uint)code < (uint)_encoding.Length ? _encoding[code] : NotFound;

    /// <summary>Gets a glyph's width from its hsbw or sbw operator.</summary>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The width in font units.</returns>
    public override float GetAdvanceWidth(int glyph)
    {
        var sink = default(NullOutlineSink);
        return RunCharstring(glyph, default, true, ref sink).Width;
    }

    /// <summary>Maps each StandardEncoding code to the glyph with that name.</summary>
    private void FillStandardEncoding()
    {
        for (var code = 0; code < _encoding.Length; code++)
        {
            var name = FontEncodings.GetGlyphName(FontEncoding.Standard, code);
            if (!name.IsEmpty)
            {
                _encoding[code] = (short)FindGlyphByName(name);
            }
        }
    }

    /// <summary>Finds the glyph a StandardEncoding code names, as seac uses.</summary>
    /// <param name="code">The StandardEncoding code.</param>
    /// <returns>The glyph id, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetStandardGlyph(int code) => FindGlyphByName(FontEncodings.GetGlyphName(FontEncoding.Standard, code));

    /// <summary>Runs a glyph's charstring.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="glyph">The glyph id.</param>
    /// <param name="origin">The glyph origin.</param>
    /// <param name="widthOnly">Whether to stop once the width is known.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>The width and any accented-character request; empty for an unknown glyph.</returns>
    private CharstringOutcome RunCharstring<TSink>(int glyph, GlyphOrigin origin, bool widthOnly, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        if ((uint)glyph >= (uint)_parts.Glyphs.Length)
        {
            return default;
        }

        // The scratch is pooled rather than stack allocated: a sink may be a ref struct, and the compiler cannot prove
        // that stack memory held by the interpreter would not escape into it.
        var scratch = ArrayPool<float>.Shared.Rent(Type1Interpreter.ScratchSize);
        try
        {
            var interpreter = new Type1Interpreter(_parts.CharData, _parts.Subrs, origin, widthOnly, scratch);
            interpreter.Run(_parts.Glyphs[glyph].Of(_parts.CharData), ref sink);
            return interpreter.Outcome;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(scratch);
        }
    }
}
