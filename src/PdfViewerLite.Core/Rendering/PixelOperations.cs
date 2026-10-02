// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Rendering;

/// <summary>Vectorised pixel helpers.</summary>
public static class PixelOperations
{
    /// <summary>The size of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The mask selecting the colour channels of a little-endian BGRA pixel.</summary>
    private const uint ColorMask = 0x00FFFFFFU;

    /// <summary>Inverts the colour channels of an opaque BGRA buffer in place, leaving alpha untouched.</summary>
    /// <param name="target">The buffer.</param>
    public static void InvertColors(RenderTarget target)
    {
        var rowBytes = target.Width * BytesPerPixel;
        for (var y = 0; y < target.Height; y++)
        {
            var row = MemoryMarshal.Cast<byte, uint>(target.Pixels.Slice(y * target.Stride, rowBytes));
            InvertColors(row);
        }
    }

    /// <summary>Inverts the colour channels of opaque BGRA pixels in place, leaving alpha untouched.</summary>
    /// <param name="pixels">The pixels.</param>
    public static void InvertColors(Span<uint> pixels)
    {
        var i = 0;
        if (Vector.IsHardwareAccelerated && pixels.Length >= Vector<uint>.Count)
        {
            var vectors = MemoryMarshal.Cast<uint, Vector<uint>>(pixels);
            var vmask = new Vector<uint>(ColorMask);
            for (var v = 0; v < vectors.Length; v++)
            {
                vectors[v] ^= vmask;
            }

            i = vectors.Length * Vector<uint>.Count;
        }

        for (; i < pixels.Length; i++)
        {
            pixels[i] ^= ColorMask;
        }
    }
}
