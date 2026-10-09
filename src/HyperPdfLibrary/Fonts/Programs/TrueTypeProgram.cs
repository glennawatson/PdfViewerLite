// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// A TrueType or OpenType font program, including the first face of a collection. Glyph outlines come from the 'glyf'
/// table, or from the 'CFF ' table of an OpenType CFF font. Damaged or missing tables give empty outlines and zero
/// metrics rather than errors.
/// </summary>
[DebuggerDisplay("TrueTypeProgram: {GlyphCount} glyphs")]
public sealed partial class TrueTypeProgram : FontProgram
{
    /// <summary>The units per em assumed when the 'head' table is missing or damaged.</summary>
    private const int DefaultUnitsPerEm = 1000;

    /// <summary>The smallest valid units per em.</summary>
    private const int MinUnitsPerEm = 16;

    /// <summary>The largest valid units per em.</summary>
    private const int MaxUnitsPerEm = 16_384;

    /// <summary>The offset of unitsPerEm in 'head'.</summary>
    private const int HeadUnitsPerEm = 18;

    /// <summary>The offset of xMin in 'head'.</summary>
    private const int HeadXMin = 36;

    /// <summary>The offset of yMin in 'head'.</summary>
    private const int HeadYMin = 38;

    /// <summary>The offset of xMax in 'head'.</summary>
    private const int HeadXMax = 40;

    /// <summary>The offset of yMax in 'head'.</summary>
    private const int HeadYMax = 42;

    /// <summary>The offset of indexToLocFormat in 'head'.</summary>
    private const int HeadLocFormat = 50;

    /// <summary>The offset of numGlyphs in 'maxp'.</summary>
    private const int MaxpGlyphCount = 4;

    /// <summary>The offset of the ascender in 'hhea'.</summary>
    private const int HheaAscender = 4;

    /// <summary>The offset of the descender in 'hhea'.</summary>
    private const int HheaDescender = 6;

    /// <summary>The offset of numberOfHMetrics in 'hhea'.</summary>
    private const int HheaMetricCount = 34;

    /// <summary>The offset of sTypoAscender in 'OS/2'.</summary>
    private const int Os2Ascender = 68;

    /// <summary>The offset of sTypoDescender in 'OS/2'.</summary>
    private const int Os2Descender = 70;

    /// <summary>The size of one long horizontal metric.</summary>
    private const int LongMetricSize = 4;

    /// <summary>The size of a long 'loca' entry.</summary>
    private const int LongLocaSize = 4;

    /// <summary>The bytes a short 'loca' entry is scaled by.</summary>
    private const int ShortLocaScale = 2;

    /// <summary>The deepest nesting of composite glyphs decoded.</summary>
    private const int MaxCompositeDepth = 8;

    /// <summary>The font data.</summary>
    private readonly ReadOnlyMemory<byte> _data;

    /// <summary>The 'loca' table.</summary>
    private readonly TableRange _loca;

    /// <summary>The 'glyf' table.</summary>
    private readonly TableRange _glyf;

    /// <summary>The 'hmtx' table.</summary>
    private readonly TableRange _hmtx;

    /// <summary>The number of long horizontal metrics.</summary>
    private readonly int _metricCount;

    /// <summary>Whether 'loca' holds 32-bit offsets.</summary>
    private readonly bool _longLoca;

    /// <summary>The chosen cmap subtables.</summary>
    private readonly CmapSelection _cmaps;

    /// <summary>The 'post' glyph names.</summary>
    private readonly PostNames _post;

    /// <summary>The CFF outlines of an OpenType CFF font.</summary>
    private readonly CffProgram? _cff;

    /// <summary>Initializes a new instance of the <see cref="TrueTypeProgram"/> class.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="directory">The table directory.</param>
    private TrueTypeProgram(ReadOnlyMemory<byte> data, SfntDirectory directory)
    {
        _data = data;
        var span = data.Span;
        var head = directory.Find(span, SfntDirectory.Head).Of(span);
        var unitsPerEm = FontBytes.U16(head, HeadUnitsPerEm);
        UnitsPerEm = unitsPerEm is >= MinUnitsPerEm and <= MaxUnitsPerEm ? unitsPerEm : DefaultUnitsPerEm;
        BoundingBox = new(FontBytes.S16(head, HeadXMin), FontBytes.S16(head, HeadYMin), FontBytes.S16(head, HeadXMax), FontBytes.S16(head, HeadYMax));
        _longLoca = FontBytes.S16(head, HeadLocFormat) == 1;
        _loca = directory.Find(span, SfntDirectory.Loca);
        _glyf = directory.Find(span, SfntDirectory.Glyf);
        _hmtx = directory.Find(span, SfntDirectory.Hmtx);
        var hhea = directory.Find(span, SfntDirectory.Hhea).Of(span);
        _metricCount = Math.Min(FontBytes.U16(hhea, HheaMetricCount), _hmtx.Length / LongMetricSize);
        (Ascent, Descent) = ReadVerticalMetrics(span, directory, hhea);
        var cffTable = directory.Find(span, SfntDirectory.Cff);
        if (_glyf.IsEmpty && !cffTable.IsEmpty && CffProgram.TryParse(data.Slice(cffTable.Offset, cffTable.Length), out var cff))
        {
            _cff = cff;
        }

        GlyphCount = CountGlyphs(span, directory);
        _cmaps = CmapSelection.Read(span, directory.Find(span, SfntDirectory.Cmap));
        _post = PostNames.Read(span, directory.Find(span, SfntDirectory.Post));
    }

    /// <inheritdoc/>
    public override int GlyphCount { get; }

    /// <inheritdoc/>
    public override float UnitsPerEm { get; }

    /// <inheritdoc/>
    public override FontMatrix FontMatrix => FontMatrix.FromScale(1 / UnitsPerEm);

    /// <inheritdoc/>
    public override PdfRectangle BoundingBox { get; }

    /// <inheritdoc/>
    public override float Ascent { get; }

    /// <inheritdoc/>
    public override float Descent { get; }

    /// <summary>Gets a value indicating whether outlines come from a CFF table.</summary>
    public bool HasCffOutlines => _cff is not null;

    /// <summary>Gets a value indicating whether the font has a Microsoft symbol (3,0) cmap.</summary>
    public bool HasSymbolCmap => _cmaps.Symbol >= 0;

    /// <summary>Parses a TrueType or OpenType font.</summary>
    /// <param name="data">The font data; the program keeps a reference to it.</param>
    /// <param name="program">The program.</param>
    /// <returns><see langword="true"/> when the data has an sfnt table directory.</returns>
    public static bool TryParse(ReadOnlyMemory<byte> data, [NotNullWhen(true)] out TrueTypeProgram? program)
    {
        program = SfntDirectory.TryRead(data.Span, out var directory) ? new TrueTypeProgram(data, directory) : null;
        return program is not null;
    }

    /// <inheritdoc/>
    public override void DecodeGlyph<TSink>(int glyph, ref TSink sink)
    {
        if (_cff is not null)
        {
            _cff.DecodeGlyph(glyph, ref sink);
            return;
        }

        DecodeGlyf(glyph, FontMatrix.Identity, 0, ref sink);
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<byte> GetGlyphName(int glyph)
    {
        var name = _post.GetName(_data.Span, glyph);
        return name.IsEmpty && _cff is not null ? _cff.GetGlyphName(glyph) : name;
    }

    /// <summary>Finds a glyph by name: first through the 'post' names, then through the Adobe Glyph List and the Unicode cmap.</summary>
    /// <param name="name">The glyph name's bytes.</param>
    /// <returns>The glyph id, or -1.</returns>
    public override int GetGlyphByName(ReadOnlySpan<byte> name)
    {
        var glyph = FindGlyphByName(name);
        if (glyph >= 0)
        {
            return glyph;
        }

        return GlyphList.TryGetCodePoint(name, out var codePoint) ? LookupUnicode(codePoint) : NotFound;
    }

    /// <summary>
    /// Finds a glyph for a code of a symbolic font: the (3,0) cmap with the code and the code in the F000, F100 and F200
    /// pages, then the (1,0) cmap, then the Unicode cmaps.
    /// </summary>
    /// <param name="code">The character code.</param>
    /// <returns>The glyph id, or -1.</returns>
    public override int GetGlyphByCharCode(int code)
    {
        var data = _data.Span;
        var glyph = LookupSymbol(data, code);
        if (glyph == 0)
        {
            glyph = TrueTypeCmap.Lookup(data, _cmaps.Mac, code);
        }

        if (glyph == 0)
        {
            glyph = TrueTypeCmap.Lookup(data, _cmaps.UnicodeBmp, code);
        }

        return ToResult(glyph);
    }

    /// <inheritdoc/>
    public override int GetGlyphByUnicode(int codePoint)
    {
        var glyph = LookupUnicode(codePoint);
        return glyph >= 0 ? glyph : base.GetGlyphByUnicode(codePoint);
    }

    /// <inheritdoc/>
    public override float GetAdvanceWidth(int glyph)
    {
        if (_metricCount == 0 || (uint)glyph >= (uint)GlyphCount)
        {
            return _cff?.GetAdvanceWidth(glyph) ?? 0;
        }

        var index = Math.Min(glyph, _metricCount - 1);
        return FontBytes.U16(_hmtx.Of(_data.Span), index * LongMetricSize);
    }

    /// <summary>Reads the ascent and descent from 'hhea', falling back to 'OS/2' and then the bounding box.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="directory">The table directory.</param>
    /// <param name="hhea">The 'hhea' table.</param>
    /// <returns>The ascent and descent.</returns>
    private VerticalMetrics ReadVerticalMetrics(ReadOnlySpan<byte> data, SfntDirectory directory, ReadOnlySpan<byte> hhea)
    {
        var ascent = FontBytes.S16(hhea, HheaAscender);
        var descent = FontBytes.S16(hhea, HheaDescender);
        if (ascent == 0 && descent == 0)
        {
            var os2 = directory.Find(data, SfntDirectory.Os2).Of(data);
            ascent = FontBytes.S16(os2, Os2Ascender);
            descent = FontBytes.S16(os2, Os2Descender);
        }

        return ascent == 0 && descent == 0 ? new(BoundingBox.Top, BoundingBox.Bottom) : new(ascent, descent);
    }

    /// <summary>Counts the glyphs from 'maxp', falling back to 'loca' or the CFF table.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="directory">The table directory.</param>
    /// <returns>The glyph count.</returns>
    private int CountGlyphs(ReadOnlySpan<byte> data, SfntDirectory directory)
    {
        var maxp = directory.Find(data, SfntDirectory.Maxp).Of(data);
        var count = FontBytes.U16(maxp, MaxpGlyphCount);
        if (_cff is not null)
        {
            return count > 0 ? Math.Min(count, _cff.GlyphCount) : _cff.GlyphCount;
        }

        var locaEntries = (_loca.Length / (_longLoca ? LongLocaSize : FontBytes.U16Size)) - 1;
        return count > 0 ? Math.Min(count, Math.Max(locaEntries, 0)) : Math.Max(locaEntries, 0);
    }

    /// <summary>Looks up a code point in the Unicode cmaps.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The glyph id, or -1.</returns>
    private int LookupUnicode(int codePoint)
    {
        var data = _data.Span;
        var glyph = TrueTypeCmap.Lookup(data, _cmaps.UnicodeFull, codePoint);
        if (glyph == 0)
        {
            glyph = TrueTypeCmap.Lookup(data, _cmaps.UnicodeBmp, codePoint);
        }

        return ToResult(glyph);
    }

    /// <summary>Looks up a code in the symbol cmap, trying the private-use pages symbol fonts use.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or zero.</returns>
    private int LookupSymbol(ReadOnlySpan<byte> data, int code)
    {
        if (_cmaps.Symbol < 0)
        {
            return 0;
        }

        ReadOnlySpan<int> pages = [0x0000, 0xF000, 0xF100, 0xF200];
        foreach (var page in pages)
        {
            var glyph = TrueTypeCmap.Lookup(data, _cmaps.Symbol, page | code);
            if (glyph != 0)
            {
                return glyph;
            }
        }

        return 0;
    }

    /// <summary>Turns a cmap result into a glyph id.</summary>
    /// <param name="glyph">The cmap result, zero for missing.</param>
    /// <returns>The glyph id, or -1.</returns>
    private int ToResult(int glyph) => glyph > 0 && glyph < GlyphCount ? glyph : NotFound;
}
