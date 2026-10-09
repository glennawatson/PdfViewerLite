// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Bounds that keep damaged or hostile JBIG2 data from exhausting memory or time.</summary>
internal static class Jbig2Limits
{
    /// <summary>The widest or tallest region, symbol or pattern decoded, as in PDFium.</summary>
    internal const int MaxImageSide = 65_535;

    /// <summary>The most pixels in one region, symbol or collective bitmap.</summary>
    internal const long MaxRegionPixels = ImageHeader.MaxPixels;

    /// <summary>The most segments one segment may refer to, as in PDFium.</summary>
    internal const int MaxReferredSegments = 64;

    /// <summary>The most symbols a symbol dictionary may export, as in PDFium.</summary>
    internal const uint MaxExportSymbols = 1 << 20;

    /// <summary>The most symbols a symbol dictionary may define, as in PDFium.</summary>
    internal const uint MaxNewSymbols = 1 << 20;

    /// <summary>The highest pattern index (GRAYMAX) a pattern dictionary may declare, as in PDFium.</summary>
    internal const uint MaxPatternIndex = 65_535;

    /// <summary>The most text region instances per byte of data, as in PDFium (an instance takes at least a quarter bit).</summary>
    internal const int InstancesPerByte = 32;

    /// <summary>The most pixels decoded or composited for one stream, which bounds the time spent on hostile data.</summary>
    internal const long MaxWork = ImageHeader.MaxPixels * 2;

    /// <summary>The most bytes of symbol and pattern bitmaps one dictionary may hold.</summary>
    internal const long MaxStoreBytes = ImageHeader.MaxPixels / 8;

    /// <summary>The largest offset at which a bitmap is composited, as in PDFium.</summary>
    internal const long MaxComposeOffset = 1 << 20;

    /// <summary>Determines whether a region size can be decoded.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns><see langword="true"/> when both sides are positive, within <see cref="MaxImageSide"/>, and the area is within <see cref="MaxRegionPixels"/>.</returns>
    internal static bool IsValidSize(long width, long height) =>
        width is > 0 and <= MaxImageSide && height is > 0 and <= MaxImageSide && width * height <= MaxRegionPixels;
}
