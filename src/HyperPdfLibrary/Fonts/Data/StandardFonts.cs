// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>
/// Metrics of the standard 14 fonts from Adobe's Core14 AFM files, and the font names PDF writers use for them. Lookups
/// search static data and do not allocate.
/// </summary>
public static partial class StandardFonts
{
    /// <summary>The length of a subset tag such as <c>ABCDEF+</c>, without the plus sign.</summary>
    private const int SubsetTagLength = 6;

    /// <summary>The longest font name the lookup normalises.</summary>
    private const int MaxFontName = 128;

    /// <summary>The italic angle is stored in tenths of a degree.</summary>
    private const float AngleScale = 10F;

    /// <summary>The position of the bounding box's left edge in a font's numbers.</summary>
    private const int BoxLeftSlot = 0;

    /// <summary>The position of the bounding box's bottom edge.</summary>
    private const int BoxBottomSlot = 1;

    /// <summary>The position of the bounding box's right edge.</summary>
    private const int BoxRightSlot = 2;

    /// <summary>The position of the bounding box's top edge.</summary>
    private const int BoxTopSlot = 3;

    /// <summary>The position of the ascent.</summary>
    private const int AscentSlot = 4;

    /// <summary>The position of the descent.</summary>
    private const int DescentSlot = 5;

    /// <summary>The position of the cap height.</summary>
    private const int CapHeightSlot = 6;

    /// <summary>The position of the x height.</summary>
    private const int XHeightSlot = 7;

    /// <summary>The position of the italic angle.</summary>
    private const int ItalicAngleSlot = 8;

    /// <summary>The position of the dominant vertical stem width.</summary>
    private const int StemVSlot = 9;

    /// <summary>The position of the dominant horizontal stem width.</summary>
    private const int StemHSlot = 10;

    /// <summary>The position of the flags.</summary>
    private const int FlagsSlot = 11;

    /// <summary>The position of the bold marker.</summary>
    private const int BoldSlot = 12;

    /// <summary>Finds a standard font by its PDF base font name, including common aliases such as Arial or TimesNewRoman,Bold.</summary>
    /// <param name="baseFontName">The /BaseFont name's bytes; a subset tag such as <c>ABCDEF+</c> is ignored.</param>
    /// <param name="metrics">The font's metrics.</param>
    /// <returns><see langword="true"/> when the name is a standard font or an alias of one.</returns>
    public static bool TryGet(ReadOnlySpan<byte> baseFontName, out StandardFontMetrics metrics)
    {
        var font = Find(baseFontName);
        metrics = new(font);
        return font != StandardFont.None;
    }

    /// <summary>Gets the metrics of a standard font.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The metrics.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StandardFontMetrics Get(StandardFont font) => new(font);

    /// <summary>Finds a standard font by its PDF base font name.</summary>
    /// <param name="baseFontName">The name's bytes.</param>
    /// <returns>The font, or <see cref="StandardFont.None"/>.</returns>
    public static StandardFont Find(ReadOnlySpan<byte> baseFontName)
    {
        var name = StripSubsetTag(baseFontName);
        if (name.IsEmpty || name.Length > MaxFontName)
        {
            return StandardFont.None;
        }

        Span<byte> normal = stackalloc byte[MaxFontName];
        var length = 0;
        foreach (var c in name)
        {
            if (c == ' ')
            {
                continue;
            }

            normal[length] = c is (byte)',' or (byte)'_' ? (byte)'-' : c;
            length++;
        }

        return FindAlias(normal[..length]);
    }

    /// <summary>Gets one of a font's stored numbers.</summary>
    /// <param name="font">The font.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The number, or zero for <see cref="StandardFont.None"/>.</returns>
    internal static int GetNumber(StandardFont font, int slot)
    {
        var index = (((int)font - 1) * NumbersPerFont) + slot;
        return (uint)index < (uint)FontNumbers.Length ? FontNumbers[index] : 0;
    }

    /// <summary>Gets a font's bounding box.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The box in thousandths of an em.</returns>
    internal static PdfRectangle GetBoundingBox(StandardFont font) =>
        new(GetNumber(font, BoxLeftSlot), GetNumber(font, BoxBottomSlot), GetNumber(font, BoxRightSlot), GetNumber(font, BoxTopSlot));

    /// <summary>Gets a font's ascent.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The ascent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetAscent(StandardFont font) => GetNumber(font, AscentSlot);

    /// <summary>Gets a font's descent.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The descent, a negative number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetDescent(StandardFont font) => GetNumber(font, DescentSlot);

    /// <summary>Gets a font's cap height.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The cap height, or zero for symbol fonts.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetCapHeight(StandardFont font) => GetNumber(font, CapHeightSlot);

    /// <summary>Gets a font's x height.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The x height, or zero for symbol fonts.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetXHeight(StandardFont font) => GetNumber(font, XHeightSlot);

    /// <summary>Gets a font's italic angle.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The angle in degrees counter-clockwise from vertical.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetItalicAngle(StandardFont font) => GetNumber(font, ItalicAngleSlot) / AngleScale;

    /// <summary>Gets a font's dominant vertical stem width.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetStemV(StandardFont font) => GetNumber(font, StemVSlot);

    /// <summary>Gets a font's dominant horizontal stem width.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetStemH(StandardFont font) => GetNumber(font, StemHSlot);

    /// <summary>Gets a font's descriptor flags.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The flags.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FontFlags GetFlags(StandardFont font) => (FontFlags)GetNumber(font, FlagsSlot);

    /// <summary>Gets whether a font is bold.</summary>
    /// <param name="font">The font.</param>
    /// <returns><see langword="true"/> when bold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBold(StandardFont font) => GetNumber(font, BoldSlot) != 0;

    /// <summary>Finds the width of a glyph.</summary>
    /// <param name="font">The font.</param>
    /// <param name="glyphName">The glyph name.</param>
    /// <param name="width">The width in thousandths of an em.</param>
    /// <returns><see langword="true"/> when the font has the glyph.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetWidth(StandardFont font, ReadOnlySpan<byte> glyphName, out float width) =>
        TryGetWidth(font, GlyphNames.Find(glyphName), out width);

    /// <summary>Finds the width of a glyph by name id.</summary>
    /// <param name="font">The font.</param>
    /// <param name="nameId">The glyph name id.</param>
    /// <param name="width">The width in thousandths of an em.</param>
    /// <returns><see langword="true"/> when the font has the glyph.</returns>
    internal static bool TryGetWidth(StandardFont font, int nameId, out float width)
    {
        width = 0;
        var index = (int)font - 1;
        if ((uint)index >= (uint)(WidthStarts.Length - 1) || nameId == GlyphNames.NoName)
        {
            return false;
        }

        int start = WidthStarts[index];
        int end = WidthStarts[index + 1];
        var found = WidthNameIds[start..end].BinarySearch((ushort)nameId);
        if (found < 0)
        {
            return false;
        }

        width = WidthValues[start + found];
        return true;
    }

    /// <summary>Removes a subset tag of six uppercase letters and a plus sign.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The name without the tag.</returns>
    internal static ReadOnlySpan<byte> StripSubsetTag(ReadOnlySpan<byte> name)
    {
        if (name.Length <= SubsetTagLength || name[SubsetTagLength] != '+')
        {
            return name;
        }

        foreach (var c in name[..SubsetTagLength])
        {
            if (c is < (byte)'A' or > (byte)'Z')
            {
                return name;
            }
        }

        return name[(SubsetTagLength + 1)..];
    }

    /// <summary>Searches the sorted alias table.</summary>
    /// <param name="name">The normalised name.</param>
    /// <returns>The font, or <see cref="StandardFont.None"/>.</returns>
    private static StandardFont FindAlias(ReadOnlySpan<byte> name)
    {
        var low = 0;
        var high = AliasTargets.Length - 1;
        while (low <= high)
        {
            var middle = (int)((uint)(low + high) >> 1);
            int start = AliasOffsets[middle];
            var order = AliasData[start..AliasOffsets[middle + 1]].SequenceCompareTo(name);
            if (order == 0)
            {
                return (StandardFont)AliasTargets[middle];
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return StandardFont.None;
    }
}
