// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>A BGRA premultiplied pixel buffer a tile is rendered into.</summary>
[DebuggerDisplay("PdfTileTarget: {Width}x{Height}")]
public readonly ref struct PdfTileTarget
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Initializes a new instance of the <see cref="PdfTileTarget"/> struct.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="stride">The bytes per row.</param>
    public PdfTileTarget(Span<byte> pixels, int width, int height, int stride)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
    }

    /// <summary>Gets the pixels.</summary>
    public Span<byte> Pixels { get; }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the bytes per row.</summary>
    public int Stride { get; }

    /// <summary>Gets a value indicating whether the buffer is large enough for its size.</summary>
    public bool IsValid
    {
        get
        {
            if (Width <= 0 || Height <= 0)
            {
                return false;
            }

            var rowBytes = (long)Width * BytesPerPixel;
            return Stride >= rowBytes && Pixels.Length >= ((long)Stride * (Height - 1)) + rowBytes;
        }
    }
}
