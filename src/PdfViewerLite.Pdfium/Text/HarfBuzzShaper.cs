// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;
using ShapingBuffer = HarfBuzzSharp.Buffer;

namespace PdfViewerLite.Pdfium.Text;

/// <summary>
/// Shapes text with HarfBuzz: ligatures, joined scripts, mark placement and OpenType kerning, from the same font bytes
/// that are embedded, so what is written matches what the font draws. One shaper is kept per font.
/// </summary>
[DebuggerDisplay("HarfBuzzShaper: {_program.Face.Family}")]
internal sealed class HarfBuzzShaper : ITextShaper, IDisposable
{
    /// <summary>The shapers made so far, one per font.</summary>
    private static readonly ConcurrentDictionary<FontProgram, HarfBuzzShaper> Shapers = new();

    /// <summary>The buffer each thread shapes into.</summary>
    [ThreadStatic]
    private static ShapingBuffer? _buffer;

    /// <summary>The font.</summary>
    private readonly FontProgram _program;

    /// <summary>The HarfBuzz font.</summary>
    private readonly Font _font;

    /// <summary>One em in font units, inverted.</summary>
    private readonly float _perUnit;

    /// <summary>Initializes a new instance of the <see cref="HarfBuzzShaper"/> class.</summary>
    /// <param name="program">The font.</param>
    private HarfBuzzShaper(FontProgram program)
    {
        _program = program;
        _perUnit = 1F / program.UnitsPerEm;

        // The font's bytes stay pinned while HarfBuzz reads them, and are unpinned when the blob is released.
        var pin = GCHandle.Alloc(program.Data, GCHandleType.Pinned);
        using var blob = new Blob(pin.AddrOfPinnedObject(), program.Data.Length, MemoryMode.ReadOnly, pin.Free);
        using var face = new Face(blob, program.Face.FaceIndex);
        _font = new(face);
        _font.SetFunctionsOpenType();
        _font.SetScale(program.UnitsPerEm, program.UnitsPerEm);
    }

    /// <inheritdoc/>
    public float Ascent => _program.Ascent;

    /// <inheritdoc/>
    public float Descent => _program.Descent;

    /// <inheritdoc/>
    public float UnderlinePosition => _program.UnderlinePosition;

    /// <inheritdoc/>
    public float UnderlineThickness => _program.UnderlineThickness;

    /// <inheritdoc/>
    public bool Shape(ReadOnlySpan<char> text, List<ShapedGlyph> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (text.IsEmpty)
        {
            return false;
        }

        var buffer = _buffer ??= new ShapingBuffer();
        buffer.ClearContents();
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        _font.Shape(buffer);
        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();
        var rightToLeft = buffer.Direction == Direction.RightToLeft;

        // HarfBuzz gives right-to-left glyphs in drawing order; layout wants reading order.
        for (var n = 0; n < infos.Length; n++)
        {
            var i = rightToLeft ? infos.Length - 1 - n : n;
            var position = positions[i];
            output.Add(new((ushort)infos[i].Codepoint, (int)infos[i].Cluster, position.XAdvance * _perUnit, position.XOffset * _perUnit, position.YOffset * _perUnit));
        }

        return rightToLeft;
    }

    /// <inheritdoc/>
    public bool Covers(int codePoint) => _program.GlyphFor(codePoint) != 0;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _font.Dispose();

    /// <summary>Gets the shaper of a font, making it on first use.</summary>
    /// <param name="program">The font.</param>
    /// <returns>The shaper.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static HarfBuzzShaper For(FontProgram program) => Shapers.GetOrAdd(program, static font => new(font));
}
