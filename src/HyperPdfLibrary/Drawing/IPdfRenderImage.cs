// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>An owned image supplied by a drawing backend.</summary>
public interface IPdfRenderImage : IDisposable
{
    /// <summary>Gets the image width in pixels.</summary>
    int Width { get; }

    /// <summary>Gets the image height in pixels.</summary>
    int Height { get; }

    /// <summary>Gets a stable identity shared by wrappers over the same image.</summary>
    uint ImageId { get; }

    /// <summary>Gets the decoded pixel storage in bytes.</summary>
    long PixelBytes { get; }

    /// <summary>Copies image pixels into caller-owned storage.</summary>
    /// <param name="layout">The requested pixel layout.</param>
    /// <param name="pixels">The destination pixels.</param>
    /// <param name="rowBytes">The destination row stride.</param>
    /// <returns>Whether the image could be read.</returns>
    bool CopyPixels(PdfImagePixelLayout layout, Span<byte> pixels, int rowBytes);
}
