// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The parameters of the halftone region decoding procedure (T.88 table 22).</summary>
[DebuggerDisplay("Jbig2HalftoneSettings: grid {GridWidth}x{GridHeight}")]
internal sealed class Jbig2HalftoneSettings
{
    /// <summary>Gets or sets the region width, HBW.</summary>
    internal int Width { get; set; }

    /// <summary>Gets or sets the region height, HBH.</summary>
    internal int Height { get; set; }

    /// <summary>Gets or sets a value indicating whether the gray-scale planes are MMR coded, HMMR.</summary>
    internal bool Mmr { get; set; }

    /// <summary>Gets or sets the generic template of the planes, HTEMPLATE.</summary>
    internal int Template { get; set; }

    /// <summary>Gets or sets a value indicating whether grid cells outside the region are skipped, HENABLESKIP.</summary>
    internal bool EnableSkip { get; set; }

    /// <summary>Gets or sets how patterns combine with the region, HCOMBOP.</summary>
    internal Jbig2ComposeOperator Operator { get; set; }

    /// <summary>Gets or sets a value indicating whether the region starts black, HDEFPIXEL.</summary>
    internal bool DefaultPixel { get; set; }

    /// <summary>Gets or sets the grid width, HGW.</summary>
    internal int GridWidth { get; set; }

    /// <summary>Gets or sets the grid height, HGH.</summary>
    internal int GridHeight { get; set; }

    /// <summary>Gets or sets the grid origin's X, HGX, in 1/256 pixels.</summary>
    internal int GridX { get; set; }

    /// <summary>Gets or sets the grid origin's Y, HGY, in 1/256 pixels.</summary>
    internal int GridY { get; set; }

    /// <summary>Gets or sets the grid vector's X, HRX, in 1/256 pixels.</summary>
    internal int VectorX { get; set; }

    /// <summary>Gets or sets the grid vector's Y, HRY, in 1/256 pixels.</summary>
    internal int VectorY { get; set; }
}
