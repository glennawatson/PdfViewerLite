// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Drawing;

/// <summary>A rectangle in drawing coordinates.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
[DebuggerDisplay("PdfRect: {Left}, {Top}, {Right}, {Bottom}")]
public readonly record struct PdfRect(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Gets an empty rectangle.</summary>
    public static PdfRect Empty => default;

    /// <summary>Gets the horizontal span.</summary>
    public float Width => Right - Left;

    /// <summary>Gets the vertical span.</summary>
    public float Height => Bottom - Top;

    /// <summary>Gets whether the rectangle has no area.</summary>
    public bool IsEmpty => Left >= Right || Top >= Bottom;

    /// <summary>Creates a rectangle from an origin and size.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The horizontal span.</param>
    /// <param name="height">The vertical span.</param>
    /// <returns>The rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfRect Create(float x, float y, float width, float height) => new(x, y, x + width, y + height);

    /// <summary>Returns a rectangle expanded on each side.</summary>
    /// <param name="dx">The horizontal amount to add to each side.</param>
    /// <param name="dy">The vertical amount to add to each side.</param>
    /// <returns>The expanded rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfRect Inflate(float dx, float dy) => new(Left - dx, Top - dy, Right + dx, Bottom + dy);
}
