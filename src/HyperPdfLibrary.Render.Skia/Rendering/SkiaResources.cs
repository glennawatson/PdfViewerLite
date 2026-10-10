// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reads native resources owned by the Skia backend.</summary>
internal static class SkiaResources
{
    /// <summary>Gets the borrowed native image.</summary>
    /// <param name="image">The owned backend image.</param>
    /// <returns>The native image.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static SKImage Image(IPdfRenderImage image) => ((SkiaRenderImage)image).Native;

    /// <summary>Gets the borrowed native recording.</summary>
    /// <param name="picture">The owned backend recording.</param>
    /// <returns>The native recording.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static SKPicture Picture(IPdfRenderPicture picture) => ((SkiaRenderPicture)picture).Native;

    /// <summary>Gets the borrowed native shader.</summary>
    /// <param name="shader">The owned backend shader.</param>
    /// <returns>The native shader.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static SKShader Shader(IPdfRenderShader shader) => ((SkiaRenderShader)shader).Native;

    /// <summary>Gets the borrowed native triangles.</summary>
    /// <param name="vertices">The owned backend mesh.</param>
    /// <returns>The native triangles.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static SKVertices Vertices(IPdfRenderVertices vertices) => ((SkiaRenderVertices)vertices).Native;
}
