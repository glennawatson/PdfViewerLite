// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The decoding state of one code-block: its area, what the packet headers said about it and where its coded data
/// sits. Lives in a pooled array and is updated in place by reference.
/// </summary>
internal record struct JpxCodeBlock
{
    /// <summary>Gets or sets the code-block's area in its sub-band's sample grid.</summary>
    internal JpxRectangle Area { get; set; }

    /// <summary>Gets or sets the index of the sub-band.</summary>
    internal int Band { get; set; }

    /// <summary>Gets or sets a value indicating whether a packet has included the code-block yet.</summary>
    internal bool Included { get; set; }

    /// <summary>Gets or sets a value indicating whether a damaged packet stopped further data for the code-block.</summary>
    internal bool Corrupted { get; set; }

    /// <summary>Gets or sets the length indicator bits, Lblock.</summary>
    internal int LengthBits { get; set; }

    /// <summary>Gets or sets the bit-planes coded: the sub-band's magnitude bits less the zero bit-planes.</summary>
    internal int BitPlanes { get; set; }

    /// <summary>Gets or sets the passes the current packet adds.</summary>
    internal int NewPasses { get; set; }

    /// <summary>Gets or sets the first codeword segment, or -1.</summary>
    internal int FirstSegment { get; set; }

    /// <summary>Gets or sets the last codeword segment, or -1.</summary>
    internal int LastSegment { get; set; }

    /// <summary>Gets or sets the first segment the current packet adds to, or -1.</summary>
    internal int PacketSegment { get; set; }

    /// <summary>Gets or sets the first data chunk, or -1.</summary>
    internal int FirstChunk { get; set; }

    /// <summary>Gets or sets the last data chunk, or -1.</summary>
    internal int LastChunk { get; set; }

    /// <summary>Gets or sets the total bytes of coded data.</summary>
    internal int DataLength { get; set; }
}
