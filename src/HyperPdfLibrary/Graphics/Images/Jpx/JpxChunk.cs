// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>A piece of a code-block's coded data within the tile data; the pieces join in order.</summary>
internal record struct JpxChunk
{
    /// <summary>Gets or sets the next chunk of the same code-block, or -1.</summary>
    internal int Next { get; set; }

    /// <summary>Gets or sets the offset in the tile data.</summary>
    internal int Offset { get; set; }

    /// <summary>Gets or sets the number of bytes.</summary>
    internal int Length { get; set; }
}
