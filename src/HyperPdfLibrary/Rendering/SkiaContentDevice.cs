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

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// The Skia device: records what the interpreter draws into an <see cref="SKPicture"/>. The picture is in the space the
/// interpreter started in (viewer space for pages), so replaying it only needs a matrix.
/// </summary>
[DebuggerDisplay("SkiaContentDevice")]
internal sealed partial class SkiaContentDevice : IPictureDevice
{
    /// <summary>The pixels of padding added to layer bounds so anti-aliased edges and strokes are not cut off.</summary>
    private const float BoundsPadding = 2;

    /// <summary>The largest alpha as a float.</summary>
    private const float AlphaMax = 255;

    /// <summary>The render modes that fill: 0, 2, 4 and 6.</summary>
    private const int FillModes = 0x55;

    /// <summary>The render modes that stroke: 1, 2, 5 and 6.</summary>
    private const int StrokeModes = 0x66;

    /// <summary>The render modes that draw nothing: 3 and 7.</summary>
    private const int HiddenModes = 0x88;

    /// <summary>The line cap value for round caps.</summary>
    private const int RoundCap = 1;

    /// <summary>The line cap value for square caps.</summary>
    private const int SquareCap = 2;

    /// <summary>The line join value for round joins.</summary>
    private const int RoundJoin = 1;

    /// <summary>The line join value for bevel joins.</summary>
    private const int BevelJoin = 2;

    /// <summary>The recorder, ended by <see cref="Finish"/>.</summary>
    private readonly SKPictureRecorder _recorder = new();

    /// <summary>The recording canvas.</summary>
    private readonly SKCanvas _canvas;

    /// <summary>The paint reused for every shape.</summary>
    private readonly SKPaint _paint = new() { IsAntialias = true };

    /// <summary>The paint reused for layers.</summary>
    private readonly SKPaint _layerPaint = new();

    /// <summary>Whether each open transparency group is a knockout group, innermost last.</summary>
    private readonly List<bool> _knockout = [];

    /// <summary>Whether <see cref="Finish"/> has run.</summary>
    private bool _finished;

    /// <summary>Initializes a new instance of the <see cref="SkiaContentDevice"/> class.</summary>
    /// <param name="cull">The area the picture covers.</param>
    internal SkiaContentDevice(SKRect cull)
        : this(cull, new())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SkiaContentDevice"/> class that counts its images with another device.</summary>
    /// <param name="cull">The area the picture covers.</param>
    /// <param name="weight">Counts the images drawn; pictures nested in another recording share its count.</param>
    private SkiaContentDevice(SKRect cull, PictureWeight weight)
    {
        Weight = weight;
        _canvas = _recorder.BeginRecording(cull, true);
    }

    /// <summary>Gets the count of the pixel memory of the images drawn, including those drawn into nested pictures.</summary>
    internal PictureWeight Weight { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Save() => _canvas.Save();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Restore() => _canvas.Restore();

    /// <inheritdoc/>
    public void Clip(SKPath path, bool evenOdd, Matrix3x2 ctm)
    {
        path.FillType = evenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(ctm));
        _canvas.ClipPath(path, SKClipOperation.Intersect, true);
    }

    /// <inheritdoc/>
    public void Fill(SKPath path, bool evenOdd, ref GraphicsState state)
    {
        path.FillType = evenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        var masked = BeginMask(ref state, path.Bounds, state.Ctm);
        FillShape(path, state.Ctm, state.Fill, state.FillAlpha, state.BlendMode);
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void Stroke(SKPath path, ref GraphicsState state)
    {
        var masked = BeginMask(ref state, Inflate(path.Bounds, state.LineWidth), state.Ctm);
        StrokeShape(path, state.Ctm, ref state);
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void DrawImage(SKImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
        Weight.Add(image);
        var matrix = new Matrix3x2(1F / image.Width, 0, 0, -1F / image.Height, 0, 1) * state.Ctm;
        var masked = BeginMask(ref state, new(0, 0, 1, 1), state.Ctm);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.BlendMode = ElementBlend(state.BlendMode);
        _paint.Color = isMask ? SkiaConversions.ToSkColor(state.Fill, state.FillAlpha) : WhiteWithAlpha(state.FillAlpha);
        var sampling = ChooseSampling(image, smooth, state.Ctm);
        _canvas.DrawImage(image, new SKRect(0, 0, image.Width, image.Height), sampling, _paint);
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        var mode = state.RenderMode;
        if (IsMode(HiddenModes, mode) || glyph.Font.GetOutline(glyph.Code) is not { } outline)
        {
            return;
        }

        var matrix = glyph.GlyphMatrix * state.Ctm;
        var masked = BeginMask(ref state, outline.Bounds, matrix);
        if (IsMode(FillModes, mode) && !state.Fill.PaintsNothing)
        {
            FillShape(outline, matrix, state.Fill, state.FillAlpha, state.BlendMode);
        }

        if (IsMode(StrokeModes, mode) && !state.Stroke.PaintsNothing)
        {
            StrokeGlyph(outline, glyph.GlyphMatrix, ref state);
        }

        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
        var masked = BeginMask(ref state, SKRect.Empty, state.Ctm);
        _ = _canvas.Save();
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(state.Ctm));
        if (shading.BBox is { } box)
        {
            _canvas.ClipRect(new(box.Left, box.Bottom, box.Right, box.Top), SKClipOperation.Intersect, true);
        }

        DrawShadingPaint(shading, state.FillAlpha, state.BlendMode, MatrixScale(state.Ctm));
        _canvas.Restore();
        EndMask(ref state, masked);
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
        _layerPaint.Reset();
        _layerPaint.BlendMode = ElementBlend(group.Blend);
        _layerPaint.Color = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)MathF.Round(Math.Clamp(group.Alpha, 0, 1) * AlphaMax));
        _canvas.SetMatrix(SKMatrix.Identity);

        // A non-isolated group starts from a copy of its backdrop, so blend modes inside it mix with what lies below. That
        // copy composites back unchanged only through a Normal blend, so other blends use an isolated layer, as PDFium does.
        if (!group.Isolated && group.Blend == PdfBlendMode.Normal)
        {
            SaveLayerWithBackdrop(group.Bounds, _layerPaint);
        }
        else
        {
            SaveLayer(group.Bounds, _layerPaint);
        }

        _knockout.Add(group.Knockout);
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
        if (group.SoftMask is { } mask)
        {
            ApplyMask(mask, group.Bounds);
        }

        _canvas.Restore();
        if (_knockout.Count > 0)
        {
            _knockout.RemoveAt(_knockout.Count - 1);
        }
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
        // Marked content has no visual effect; optional content is resolved by the interpreter.
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
        // See BeginMarkedContent.
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(SKRect cull) => new SkiaContentDevice(cull, Weight);

    /// <inheritdoc/>
    public SKPicture Finish()
    {
        _finished = true;
        return _recorder.EndRecording();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_finished)
        {
            using var discarded = _recorder.EndRecording();
            _finished = true;
        }

        _paint.Dispose();
        _layerPaint.Dispose();
        _canvas.Dispose();
        _recorder.Dispose();
    }

    /// <summary>Determines whether a text render mode is in a set.</summary>
    /// <param name="modes">A bit set with bit n set for render mode n.</param>
    /// <param name="mode">The render mode, 0 to 7.</param>
    /// <returns><see langword="true"/> when the mode is in the set.</returns>
    private static bool IsMode(int modes, int mode) => ((modes >> mode) & 1) != 0;

    /// <summary>Grows a rectangle for a stroke.</summary>
    /// <param name="bounds">The path bounds.</param>
    /// <param name="width">The line width.</param>
    /// <returns>The grown rectangle.</returns>
    private static SKRect Inflate(SKRect bounds, float width)
    {
        var grow = Math.Max(width, 1) * BoundsPadding;
        return new(bounds.Left - grow, bounds.Top - grow, bounds.Right + grow, bounds.Bottom + grow);
    }

    /// <summary>Starts a layer, limited to a rectangle unless it is empty.</summary>
    /// <param name="bounds">The area in page space, or empty for no limit.</param>
    /// <param name="paint">The layer paint.</param>
    private void SaveLayer(SKRect bounds, SKPaint paint)
    {
        if (bounds.IsEmpty)
        {
            _ = _canvas.SaveLayer(paint);
        }
        else
        {
            _ = _canvas.SaveLayer(Inflate(bounds, 0), paint);
        }
    }

    /// <summary>Starts a layer that begins as a copy of what lies beneath it, limited to a rectangle unless it is empty.</summary>
    /// <param name="bounds">The area in page space, or empty for no limit.</param>
    /// <param name="paint">The layer paint.</param>
    private void SaveLayerWithBackdrop(SKRect bounds, SKPaint paint)
    {
        var record = new SKCanvasSaveLayerRec { Paint = paint, Flags = SKCanvasSaveLayerRecFlags.InitializeWithPrevious };
        if (!bounds.IsEmpty)
        {
            record.Bounds = Inflate(bounds, 0);
        }

        _ = _canvas.SaveLayer(record);
    }

    /// <summary>
    /// Gets the Skia blend mode for an object. Inside a knockout group a Normal object replaces what the group drew before
    /// it, which is the Source mode weighted by the object's coverage.
    /// </summary>
    /// <param name="blend">The object's blend mode.</param>
    /// <returns>The Skia blend mode.</returns>
    private SKBlendMode ElementBlend(PdfBlendMode blend) =>
        blend == PdfBlendMode.Normal && _knockout.Count > 0 && _knockout[^1] ? SKBlendMode.Src : SkiaConversions.ToSkBlend(blend);

    /// <summary>Strokes a glyph outline in user space, so the line width follows the page transform rather than the font size.</summary>
    /// <param name="outline">The outline in glyph space.</param>
    /// <param name="glyphMatrix">The matrix from glyph space to user space.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeGlyph(SKPath outline, Matrix3x2 glyphMatrix, ref GraphicsState state)
    {
        using var transformed = new SKPath();
        outline.Transform(SkiaConversions.ToSkMatrix(glyphMatrix), transformed);
        StrokeShape(transformed, state.Ctm, ref state);
    }
}
