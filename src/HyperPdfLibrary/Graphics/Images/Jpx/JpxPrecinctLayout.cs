// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The code-blocks of one precinct within one sub-band, and its two tag trees.</summary>
/// <param name="BlocksWide">The code-blocks across.</param>
/// <param name="BlocksHigh">The code-blocks down.</param>
/// <param name="FirstBlock">The index of the first code-block; the rest follow in raster order.</param>
/// <param name="InclusionTree">The first node of the inclusion tag tree.</param>
/// <param name="ZeroPlaneTree">The first node of the zero bit-plane tag tree.</param>
internal readonly record struct JpxPrecinctLayout(int BlocksWide, int BlocksHigh, int FirstBlock, int InclusionTree, int ZeroPlaneTree)
{
    /// <summary>Gets the number of code-blocks.</summary>
    internal int BlockCount => BlocksWide * BlocksHigh;
}
