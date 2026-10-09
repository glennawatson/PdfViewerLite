// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using SkiaSharp;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// A text device that records which character codes each font shows, so a font can be cut down to those glyphs. It
/// runs like text extraction: images are never decoded and every layer is read.
/// </summary>
[DebuggerDisplay("FontUsageDevice: {Codes.Count} fonts")]
internal sealed class FontUsageDevice : ITextObjectDevice
{
    /// <summary>Gets the codes each font shows.</summary>
    internal Dictionary<PdfFont, HashSet<int>> Codes { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        ref var codes = ref CollectionsMarshal.GetValueRefOrAddDefault(Codes, glyph.Font, out _);
        codes ??= [];
        _ = codes.Add(glyph.Code);
    }

    /// <inheritdoc/>
    public void BeginTextObject()
    {
    }

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
}
