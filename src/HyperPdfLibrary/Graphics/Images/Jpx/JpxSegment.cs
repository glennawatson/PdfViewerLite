// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One codeword segment of a code-block: coding passes that share one arithmetic or raw coder run.</summary>
internal record struct JpxSegment
{
    /// <summary>Gets or sets the next segment of the same code-block, or -1.</summary>
    internal int Next { get; set; }

    /// <summary>Gets or sets the most passes the segment may hold.</summary>
    internal int MaxPasses { get; set; }

    /// <summary>Gets or sets the passes received.</summary>
    internal int Passes { get; set; }

    /// <summary>Gets or sets the bytes received.</summary>
    internal int Length { get; set; }

    /// <summary>Gets or sets the passes the current packet adds.</summary>
    internal int NewPasses { get; set; }

    /// <summary>Gets or sets the bytes the current packet adds.</summary>
    internal int NewLength { get; set; }
}
