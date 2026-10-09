// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A thread's reusable raster surface and paints. The surface only grows, so rendering tiles of one size allocates nothing
/// after the first.
/// </summary>
[DebuggerDisplay("RenderSurface: {Width}x{Height}")]
internal sealed class RenderSurface : IDisposable
{
    /// <summary>The weight of red in luminance.</summary>
    private const float LumaRed = 0.299F;

    /// <summary>The weight of green in luminance.</summary>
    private const float LumaGreen = 0.587F;

    /// <summary>The weight of blue in luminance.</summary>
    private const float LumaBlue = 0.114F;

    /// <summary>The surface for the current thread.</summary>
    [ThreadStatic]
    private static RenderSurface? _current;

    /// <summary>The surface, replaced when a larger one is needed.</summary>
    private SKSurface? _surface;

    /// <summary>The canvas of <see cref="_surface"/>.</summary>
    private SKCanvas? _canvas;

    /// <summary>Initializes a new instance of the <see cref="RenderSurface"/> class.</summary>
    internal RenderSurface()
    {
        float[] gray =
        [
            LumaRed, LumaGreen, LumaBlue, 0, 0,
            LumaRed, LumaGreen, LumaBlue, 0, 0,
            LumaRed, LumaGreen, LumaBlue, 0, 0,
            0, 0, 0, 1, 0,
        ];
        GrayPaint = new() { ColorFilter = SKColorFilter.CreateColorMatrix(gray) };
    }

    /// <summary>Gets the current thread's surface holder.</summary>
    internal static RenderSurface Current => _current ??= new();

    /// <summary>Gets the width of the surface.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the height of the surface.</summary>
    internal int Height { get; private set; }

    /// <summary>Gets the paint that renders a layer in grayscale.</summary>
    internal SKPaint GrayPaint { get; }

    /// <summary>Gets the canvas of a surface at least as large as requested.</summary>
    /// <param name="width">The width needed.</param>
    /// <param name="height">The height needed.</param>
    /// <returns>The canvas.</returns>
    internal SKCanvas GetCanvas(int width, int height)
    {
        if (_canvas is not null && width <= Width && height <= Height)
        {
            return _canvas;
        }

        _surface?.Dispose();
        Width = Math.Max(width, Width);
        Height = Math.Max(height, Height);
        _surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        _canvas = _surface.Canvas;
        return _canvas;
    }

    /// <summary>Copies the top-left of the surface into a pixel buffer.</summary>
    /// <param name="info">The size and format of the destination.</param>
    /// <param name="pixels">The destination.</param>
    /// <param name="stride">The bytes per row.</param>
    /// <returns><see langword="true"/> when the pixels were copied.</returns>
    internal bool ReadPixels(SKImageInfo info, nint pixels, int stride) =>
        _surface?.ReadPixels(info, pixels, stride, 0, 0) == true;

    /// <inheritdoc/>
    void IDisposable.Dispose()
    {
        _surface?.Dispose();
        GrayPaint.Dispose();
    }
}
