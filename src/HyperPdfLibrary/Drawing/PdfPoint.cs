// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Drawing;

/// <summary>A point in a drawing path.</summary>
/// <param name="X">The horizontal coordinate.</param>
/// <param name="Y">The vertical coordinate.</param>
[DebuggerDisplay("PdfPoint: {X}, {Y}")]
public readonly record struct PdfPoint(float X, float Y)
{
    /// <summary>Gets the distance between two points.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns>The Euclidean distance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(PdfPoint left, PdfPoint right) =>
        Vector2.Distance(new(left.X, left.Y), new(right.X, right.Y));
}
