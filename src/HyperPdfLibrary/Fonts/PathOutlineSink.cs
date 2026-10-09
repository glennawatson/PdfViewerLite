// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Programs;
using SkiaSharp;

namespace HyperPdfLibrary.Fonts;

/// <summary>Writes a decoded glyph outline into a Skia path builder, transforming font units to glyph space.</summary>
/// <param name="Builder">The path builder.</param>
/// <param name="Transform">The transform from font units to glyph space.</param>
[DebuggerDisplay("PathOutlineSink")]
internal readonly record struct PathOutlineSink(SKPathBuilder Builder, FontMatrix Transform) : IGlyphOutlineSink
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveTo(float x, float y) => Builder.MoveTo(Transform.TransformX(x, y), Transform.TransformY(x, y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LineTo(float x, float y) => Builder.LineTo(Transform.TransformX(x, y), Transform.TransformY(x, y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void QuadraticTo(float controlX, float controlY, float x, float y) =>
        Builder.QuadTo(Transform.TransformX(controlX, controlY), Transform.TransformY(controlX, controlY), Transform.TransformX(x, y), Transform.TransformY(x, y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y) =>
        Builder.CubicTo(
            Transform.TransformX(control1X, control1Y),
            Transform.TransformY(control1X, control1Y),
            Transform.TransformX(control2X, control2Y),
            Transform.TransformY(control2X, control2Y),
            Transform.TransformX(x, y),
            Transform.TransformY(x, y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => Builder.Close();
}
