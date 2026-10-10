// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Owns a native image while exposing its managed layout.</summary>
internal sealed class SkiaRenderImage : IPdfRenderImage
{
    /// <summary>The owned native image.</summary>
    private SKImage? _native;

    /// <summary>Initializes a new instance of the <see cref="SkiaRenderImage"/> class.</summary>
    /// <param name="native">The owned image.</param>
    internal SkiaRenderImage(SKImage native)
    {
        _native = native;
        Width = native.Width;
        Height = native.Height;
        ImageId = native.UniqueId;
        PixelBytes = (long)Width * Height * native.Info.BytesPerPixel;
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public uint ImageId { get; }

    /// <inheritdoc/>
    public long PixelBytes { get; }

    /// <summary>Gets the borrowed native resource while it is owned.</summary>
    /// <exception cref="ObjectDisposedException">The image has been disposed.</exception>
    internal SKImage Native => Volatile.Read(ref _native) ?? throw new ObjectDisposedException(nameof(SkiaRenderImage));

    /// <inheritdoc/>
    public unsafe bool CopyPixels(PdfImagePixelLayout layout, Span<byte> pixels, int rowBytes)
    {
        if (layout.Width <= 0 || layout.Height <= 0)
        {
            return false;
        }

        var info = SkiaRenderBackend.ImageInfo(layout);
        var visibleRow = (long)layout.Width * info.BytesPerPixel;
        var required = ((long)(layout.Height - 1) * rowBytes) + visibleRow;
        if (rowBytes < visibleRow || required > pixels.Length)
        {
            return false;
        }

        fixed (byte* pointer = pixels)
        {
            var image = Native;
            if (rowBytes % info.BytesPerPixel == 0)
            {
                return image.ReadPixels(info, (nint)pointer, rowBytes, 0, 0);
            }

            var rowInfo = new SKImageInfo(info.Width, 1, info.ColorType, info.AlphaType);
            for (var row = 0; row < info.Height; row++)
            {
                if (!image.ReadPixels(rowInfo, (nint)(pointer + (row * rowBytes)), (int)visibleRow, 0, row))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _native, null)?.Dispose();
}
