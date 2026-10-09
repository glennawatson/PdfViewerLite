// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <content>Rendering with the managed library's Skia renderer.</content>
public sealed partial class HyperPdfDocument
{
    /// <summary>The renderer, made on first use and replaced after each edit so no picture of the old page is replayed.</summary>
    private PdfPageRenderer? _renderer;

    /// <summary>Gets the current renderer, making one when there is none.</summary>
    private PdfPageRenderer Renderer
    {
        get
        {
            if (Volatile.Read(ref _renderer) is { } existing)
            {
                return existing;
            }

            var created = new PdfPageRenderer(_document, CreateRenderOptions());
            if (Interlocked.CompareExchange(ref _renderer, created, null) is { } winner)
            {
                created.Dispose();
                return winner;
            }

            return created;
        }
    }

    /// <inheritdoc/>
    public bool Render(in PageRenderInfo info, RenderTarget target)
    {
        if (IsDisposed || (uint)info.PageIndex >= (uint)PageCount)
        {
            return false;
        }

        var request = new PdfTileRequest(info.PageIndex, info.Scale, (int)info.Rotation, info.OffsetX, info.OffsetY, ToLibraryFlags(info.Flags));
        var pixels = new PdfTileTarget(target.Pixels, target.Width, target.Height, target.Stride);
        try
        {
            return Renderer.Render(request, pixels);
        }
        catch (ObjectDisposedException) when (!IsDisposed)
        {
            // An edit replaced the renderer while this tile started; draw it once more with the new one.
            return Renderer.Render(request, pixels);
        }
    }

    /// <summary>Maps viewer render flags to library render flags.</summary>
    /// <param name="flags">The viewer flags.</param>
    /// <returns>The library flags.</returns>
    private static PdfRenderFlags ToLibraryFlags(RenderFlags flags)
    {
        var result = PdfRenderFlags.None;
        if ((flags & RenderFlags.Annotations) != 0)
        {
            result |= PdfRenderFlags.Annotations;
        }

        if ((flags & RenderFlags.Grayscale) != 0)
        {
            result |= PdfRenderFlags.Grayscale;
        }

        if ((flags & RenderFlags.Printing) != 0)
        {
            result |= PdfRenderFlags.Printing;
        }

        if ((flags & RenderFlags.FixedDeviceColors) != 0)
        {
            result |= PdfRenderFlags.FixedDeviceColors;
        }

        return result;
    }

    /// <summary>Drops the renderer and its page pictures; tiles drawing with it finish, and the next tile makes a new one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResetRenderer() => Interlocked.Exchange(ref _renderer, null)?.Dispose();
}
