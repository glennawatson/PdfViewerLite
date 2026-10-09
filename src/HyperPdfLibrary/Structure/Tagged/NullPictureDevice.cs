// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// A picture device that keeps nothing, for the pattern cells and soft masks the interpreter records while
/// <see cref="MarkedContentRecorder"/> runs a page: they draw no page text, so they need no recording.
/// </summary>
/// <param name="cull">The area the picture covers.</param>
[DebuggerDisplay("NullPictureDevice")]
internal sealed class NullPictureDevice(SKRect cull) : IPictureDevice
{
    /// <inheritdoc/>
    public void Save()
    {
    }

    /// <inheritdoc/>
    public void Restore()
    {
    }

    /// <inheritdoc/>
    public void Fill(SKPath path, bool evenOdd, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Stroke(SKPath path, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Clip(SKPath path, bool evenOdd, Matrix3x2 ctm)
    {
    }

    /// <inheritdoc/>
    public void DrawImage(SKImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(SKRect cull) => new NullPictureDevice(cull);

    /// <inheritdoc/>
    public SKPicture Finish()
    {
        using var recorder = new SKPictureRecorder();
        _ = recorder.BeginRecording(cull);
        return recorder.EndRecording();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
