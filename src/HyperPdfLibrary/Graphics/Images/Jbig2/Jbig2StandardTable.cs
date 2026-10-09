// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The numbers of the standard Huffman tables of T.88 annex B.</summary>
internal static class Jbig2StandardTable
{
    /// <summary>Table B.1: sizes, aggregate counts and export runs.</summary>
    internal const int B1 = 1;

    /// <summary>Table B.2: symbol widths.</summary>
    internal const int B2 = 2;

    /// <summary>Table B.3: symbol widths with negative deltas.</summary>
    internal const int B3 = 3;

    /// <summary>Table B.4: height class deltas.</summary>
    internal const int B4 = 4;

    /// <summary>Table B.5: height class deltas with negative deltas.</summary>
    internal const int B5 = 5;

    /// <summary>Table B.6: first S of a strip.</summary>
    internal const int B6 = 6;

    /// <summary>Table B.7: first S of a strip, alternative.</summary>
    internal const int B7 = 7;

    /// <summary>Table B.8: S deltas.</summary>
    internal const int B8 = 8;

    /// <summary>Table B.9: S deltas, alternative.</summary>
    internal const int B9 = 9;

    /// <summary>Table B.10: S deltas, second alternative.</summary>
    internal const int B10 = 10;

    /// <summary>Table B.11: strip T deltas.</summary>
    internal const int B11 = 11;

    /// <summary>Table B.12: strip T deltas, alternative.</summary>
    internal const int B12 = 12;

    /// <summary>Table B.13: strip T deltas, second alternative.</summary>
    internal const int B13 = 13;

    /// <summary>Table B.14: refinement deltas.</summary>
    internal const int B14 = 14;

    /// <summary>Table B.15: refinement deltas, alternative.</summary>
    internal const int B15 = 15;
}
