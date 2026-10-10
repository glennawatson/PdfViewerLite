// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reuses one thread's native wrappers for drawing into pinned caller pixels.</summary>
internal sealed class SkiaDrawingSession : IPdfDrawingSession
{
    /// <summary>The drawing session isolated to the current thread.</summary>
    [ThreadStatic]
    private static SkiaDrawingSession? _current;

    /// <summary>The thread's reusable native wrappers.</summary>
    private readonly RenderSurface _surface = RenderSurface.Current;

    /// <summary>Gets the reusable session for the current thread.</summary>
    internal static SkiaDrawingSession Current => _current ??= new();

    /// <inheritdoc/>
    public unsafe bool DrawPage(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, PdfTileTarget target, bool grayscale)
    {
        BorrowedPixelDrawing.CheckAvailable(_surface);
        fixed (byte* pixels = target.Pixels)
        {
            try
            {
                if (!BorrowedPixelDrawing.Attach(_surface, target, (nint)pixels))
                {
                    return false;
                }

                DrawPageContent(content, annotations, matrix, target, grayscale);
                return true;
            }
            finally
            {
                BorrowedPixelDrawing.Detach(_surface);
            }
        }
    }

    /// <inheritdoc/>
    public unsafe bool DrawImage(IPdfRenderImage image, PdfTileTarget target)
    {
        BorrowedPixelDrawing.CheckAvailable(_surface);
        fixed (byte* pixels = target.Pixels)
        {
            try
            {
                if (!BorrowedPixelDrawing.Attach(_surface, target, (nint)pixels))
                {
                    return false;
                }

                DrawImageContent(image, target);
                return true;
            }
            finally
            {
                BorrowedPixelDrawing.Detach(_surface);
            }
        }
    }

    /// <summary>Replays recordings into the current borrowed target.</summary>
    /// <param name="content">The content recording.</param>
    /// <param name="annotations">The optional annotation recording.</param>
    /// <param name="matrix">The page transform.</param>
    /// <param name="target">The attached target.</param>
    /// <param name="grayscale">Whether to convert the replay to grayscale.</param>
    private void DrawPageContent(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, PdfTileTarget target, bool grayscale)
    {
        var canvas = _surface.Canvas;
        var transform = SkiaConversions.ToSkMatrix(matrix);
        var saved = canvas.Save();
        canvas.ClipRect(new(0, 0, target.Width, target.Height));
        canvas.Clear(SKColors.White);
        if (grayscale)
        {
            _ = canvas.SaveLayer(_surface.GrayPaint);
        }

        canvas.DrawPicture(SkiaResources.Picture(content), in transform);
        if (annotations is not null)
        {
            canvas.DrawPicture(SkiaResources.Picture(annotations), in transform);
        }

        canvas.RestoreToCount(saved);
    }

    /// <summary>Fits an image into the current borrowed target.</summary>
    /// <param name="image">The image.</param>
    /// <param name="target">The attached target.</param>
    private void DrawImageContent(IPdfRenderImage image, PdfTileTarget target)
    {
        var canvas = _surface.Canvas;
        var saved = canvas.Save();
        canvas.ResetMatrix();
        canvas.ClipRect(new(0, 0, target.Width, target.Height));
        canvas.Clear(SKColors.White);
        canvas.DrawImage(SkiaResources.Image(image), new SKRect(0, 0, target.Width, target.Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.RestoreToCount(saved);
    }
}
