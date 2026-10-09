// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Shadings;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <content>Paint setup, patterns, meshes and soft masks.</content>
internal sealed partial class SkiaContentDevice
{
    /// <summary>The factor that makes an odd-length dash array even by repeating it.</summary>
    private const int PairLength = 2;

    /// <summary>
    /// The page width, in points, below which a stroke also gets a hairline: lines this thin fall under one pixel at
    /// 100% zoom and above. Wider lines get none, because a hairline under an anti-aliased one point line darkens its
    /// edges, so below 100% zoom lines of one point or more may draw thinner than a pixel.
    /// </summary>
    private const float ThinLineLimit = 1;

    /// <summary>The dash lengths PDFium treats as zero.</summary>
    private const float MinDashLength = 0.000001F;

    /// <summary>The length PDFium gives a zero dash, so caps still draw a dot.</summary>
    private const float ZeroDashLength = 0.1F;

    /// <summary>The shortest dash cycle, in device units, that PDFium dashes; shorter patterns are drawn solid.</summary>
    private const float MinDashCycle = 0.1F;

    /// <summary>The smallest scale used when dividing by a matrix scale.</summary>
    private const float MinScale = 1e-6F;

    /// <summary>The colour channel table that leaves a channel unchanged.</summary>
    private static readonly byte[] IdentityTable = CreateIdentityTable();

    /// <summary>Creates the filter that turns a colour's luminosity into alpha.</summary>
    /// <param name="transfer">The transfer table, or null.</param>
    /// <returns>The filter.</returns>
    private static SKColorFilter CreateLumaFilter(byte[]? transfer)
    {
        var luma = SKColorFilter.CreateLumaColor();
        if (transfer is null)
        {
            return luma;
        }

        // SkiaSharp rejects null channel tables, so the colour channels get the identity table.
        using var table = SKColorFilter.CreateTable(transfer, IdentityTable, IdentityTable, IdentityTable);
        var composed = SKColorFilter.CreateCompose(table, luma);
        luma.Dispose();
        return composed;
    }

    /// <summary>Creates the identity channel table.</summary>
    /// <returns>256 entries, each its own index.</returns>
    private static byte[] CreateIdentityTable()
    {
        var table = new byte[byte.MaxValue + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)i;
        }

        return table;
    }

    /// <summary>Creates white at an alpha, which lets a shader's colours through scaled by the alpha.</summary>
    /// <param name="alpha">The alpha from 0 to 1.</param>
    /// <returns>The colour.</returns>
    private static SKColor WhiteWithAlpha(float alpha) =>
        new(byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)MathF.Round(Math.Clamp(alpha, 0, 1) * AlphaMax));

    /// <summary>Chooses how an image is resampled.</summary>
    /// <param name="image">The image.</param>
    /// <param name="smooth">Whether the image asks for smoothing.</param>
    /// <param name="ctm">The matrix from the image's unit square to the page.</param>
    /// <returns>The sampling options.</returns>
    private static SKSamplingOptions ChooseSampling(SKImage image, bool smooth, Matrix3x2 ctm)
    {
        var width = MathF.Sqrt((ctm.M11 * ctm.M11) + (ctm.M12 * ctm.M12));
        return smooth || image.Width >= width
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }

    /// <summary>
    /// Creates a dash effect the way PDFium dashes: a length of zero (or less than a millionth) becomes 0.1 so a zero
    /// dash with round or square caps still draws a dot, negative lengths count as their size, and a pattern whose
    /// cycle spans less than a tenth of a device unit is drawn solid. Negative lengths count as zero.
    /// </summary>
    /// <param name="lengths">The dash lengths; empty for a solid line.</param>
    /// <param name="phase">The phase.</param>
    /// <param name="scale">The device units one unit of the path's space spans.</param>
    /// <returns>The effect, or null for a solid line.</returns>
    private static SKPathEffect? CreateDash(ImmutableArray<float> lengths, float phase, float scale)
    {
        if (lengths.IsDefaultOrEmpty)
        {
            return null;
        }

        var source = ImmutableCollectionsMarshal.AsArray(lengths)!;
        var cycle = 0F;
        foreach (var length in source)
        {
            if (!float.IsFinite(length))
            {
                return null;
            }

            cycle += Math.Max(0, length);
        }

        if (cycle * scale < MinDashCycle)
        {
            return null;
        }

        var count = source.Length % PairLength == 0 ? source.Length : source.Length * PairLength;
        var array = new float[count];
        for (var i = 0; i < count; i++)
        {
            var length = source[i % source.Length];
            array[i] = length <= MinDashLength ? ZeroDashLength : length;
        }

        return SKPathEffect.CreateDash(array, phase);
    }

    /// <summary>Creates the repeating shader of a tiling pattern's period.</summary>
    /// <param name="tile">The period.</param>
    /// <param name="local">The matrix from pattern space to the space the shape is drawn in.</param>
    /// <returns>The shader, or null.</returns>
    private static SKShader? CreateTileShader(PatternCell tile, SKMatrix local)
    {
        if (tile.Image is { } image)
        {
            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            return image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, tile.ImageToPattern.PostConcat(local));
        }

        return tile.Picture?.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, SKFilterMode.Linear, local, tile.Tile);
    }

    /// <summary>Gets the average scale of a matrix: how many page units one unit of its space spans.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The scale.</returns>
    private static float MatrixScale(Matrix3x2 matrix) =>
        (MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)) + MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22))) / PairLength;

    /// <summary>Opens a layer when the state has a soft mask.</summary>
    /// <param name="state">The graphics state.</param>
    /// <param name="localBounds">The area to be painted in the space of <paramref name="matrix"/>, or empty if unknown.</param>
    /// <param name="matrix">The matrix from that space to the page.</param>
    /// <returns>The scope to give to <see cref="EndMask"/>.</returns>
    private MaskScope BeginMask(ref GraphicsState state, SKRect localBounds, Matrix3x2 matrix)
    {
        if (state.SoftMask is null)
        {
            return default;
        }

        var bounds = localBounds.IsEmpty ? SKRect.Empty : SkiaConversions.ToSkMatrix(matrix).MapRect(localBounds);
        _canvas.SetMatrix(SKMatrix.Identity);
        _layerPaint.Reset();
        SaveLayer(bounds, _layerPaint);
        return new(true, bounds);
    }

    /// <summary>Applies the soft mask to what was painted since <see cref="BeginMask"/>, and closes the layer.</summary>
    /// <param name="state">The graphics state.</param>
    /// <param name="scope">The scope from <see cref="BeginMask"/>.</param>
    private void EndMask(ref GraphicsState state, in MaskScope scope)
    {
        if (!scope.Active || state.SoftMask is not { } mask)
        {
            return;
        }

        ApplyMask(mask, scope.Bounds);
        _canvas.Restore();
    }

    /// <summary>Multiplies the open layer by a soft mask's coverage.</summary>
    /// <param name="mask">The mask.</param>
    /// <param name="bounds">The layer's area in page space, or empty.</param>
    private void ApplyMask(PdfSoftMask mask, SKRect bounds)
    {
        _canvas.SetMatrix(SKMatrix.Identity);
        using var filter = mask.IsLuminosity ? CreateLumaFilter(mask.Transfer) : null;
        _layerPaint.Reset();
        _layerPaint.BlendMode = SKBlendMode.DstIn;
        _layerPaint.ColorFilter = filter;
        SaveLayer(bounds, _layerPaint);
        if (mask.IsLuminosity && mask.Backdrop != SKColors.Black)
        {
            _paint.Reset();
            _paint.Color = mask.Backdrop;
            _canvas.DrawPaint(_paint);
        }

        mask.Picture.Playback(_canvas);
        _canvas.Restore();
    }

    /// <summary>Sets the paint's colour or shader for a fill or stroke colour.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="matrix">The matrix the shape is drawn with, from shape space to the page.</param>
    /// <returns>A shader to dispose after drawing, or null.</returns>
    private SKShader? ApplyColor(in ColorState color, float alpha, Matrix3x2 matrix)
    {
        if (color.Pattern is not { } pattern)
        {
            _paint.Color = SkiaConversions.ToSkColor(color, alpha);
            return null;
        }

        _paint.Color = WhiteWithAlpha(alpha);
        if (!Matrix3x2.Invert(matrix, out var inverse))
        {
            return null;
        }

        var local = SkiaConversions.ToSkMatrix(pattern.Matrix * inverse);
        SKShader? shader = null;
        if (pattern.Tile is { } tile)
        {
            shader = CreateTileShader(tile, local);
        }
        else if (pattern.Shading?.GetShader() is { } source)
        {
            shader = source.WithLocalMatrix(local);
        }

        _paint.Shader = shader;
        return shader;
    }

    /// <summary>Fills a shape.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="color">The colour.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    private void FillShape(SKPath path, Matrix3x2 matrix, in ColorState color, float alpha, PdfBlendMode blend)
    {
        if (color.Pattern is { Shading.IsMesh: true } meshPattern)
        {
            FillMesh(path, matrix, meshPattern, alpha, blend);
            return;
        }

        if (color.Pattern?.Shading?.Background is { } background)
        {
            _paint.Reset();
            _paint.IsAntialias = true;
            _paint.BlendMode = ElementBlend(blend);
            _paint.Color = SkiaConversions.ToSkColor(new(PdfColorSpace.DeviceRgb, background, null, false), alpha);
            _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
            _canvas.DrawPath(path, _paint);
        }

        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.BlendMode = ElementBlend(blend);
        using var shader = ApplyColor(color, alpha, matrix);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.DrawPath(path, _paint);
    }

    /// <summary>Strokes a shape with the state's line style.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page; the line width is scaled by it.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeShape(SKPath path, Matrix3x2 matrix, ref GraphicsState state)
    {
        var scale = MatrixScale(matrix);
        if (state.Stroke.Pattern is { Shading.IsMesh: true } meshPattern)
        {
            StrokeMesh(path, matrix, meshPattern, scale, ref state);
            return;
        }

        // PDFium never draws a line thinner than one device pixel. The picture is replayed at any scale, so a line that
        // could fall below a pixel is drawn twice: at its width, and as a hairline, which Skia keeps one pixel wide.
        var thin = state.LineWidth > 0 && state.LineWidth * scale < ThinLineLimit;
        if (!thin)
        {
            DrawStroke(path, matrix, scale, ref state, new(state.StrokeAlpha, ElementBlend(state.BlendMode), false));
            return;
        }

        var layered = state.StrokeAlpha < 1 || state.BlendMode != PdfBlendMode.Normal;
        if (layered)
        {
            BeginStrokeLayer(path, matrix, ref state);
        }

        var alpha = layered ? 1 : state.StrokeAlpha;
        var blend = layered ? SKBlendMode.SrcOver : ElementBlend(state.BlendMode);
        DrawStroke(path, matrix, scale, ref state, new(alpha, blend, true));
        if (layered)
        {
            _canvas.Restore();
        }
    }

    /// <summary>Sets up the stroke paint and draws a path, and then its hairline when asked.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="scale">The matrix's average scale.</param>
    /// <param name="state">The graphics state.</param>
    /// <param name="pass">The alpha and blend to stroke with, and whether to add a hairline.</param>
    private void DrawStroke(SKPath path, Matrix3x2 matrix, float scale, ref GraphicsState state, in StrokePass pass)
    {
        ConfigureStroke(ref state);
        _paint.BlendMode = pass.Blend;
        using var dash = CreateDash(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        using var shader = ApplyColor(state.Stroke, pass.Alpha, matrix);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.DrawPath(path, _paint);
        if (!pass.Hairline)
        {
            return;
        }

        _paint.StrokeWidth = 0;
        _canvas.DrawPath(path, _paint);
    }

    /// <summary>Resets the paint to stroke with the state's width, caps, joins and miter limit.</summary>
    /// <param name="state">The graphics state.</param>
    private void ConfigureStroke(ref GraphicsState state)
    {
        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.Style = SKPaintStyle.Stroke;
        _paint.StrokeWidth = state.LineWidth;

        // Skia ignores a negative limit and keeps its default, so clamp; a limit below 1 bevels every corner, as in PDFium.
        _paint.StrokeMiter = Math.Max(0, state.MiterLimit);
        _paint.StrokeCap = state.LineCap switch
        {
            RoundCap => SKStrokeCap.Round,
            SquareCap => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt,
        };
        _paint.StrokeJoin = state.LineJoin switch
        {
            RoundJoin => SKStrokeJoin.Round,
            BevelJoin => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter,
        };
    }

    /// <summary>Opens a layer that composites a thin translucent stroke once, so its hairline and body do not double up.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="state">The graphics state.</param>
    private void BeginStrokeLayer(SKPath path, Matrix3x2 matrix, ref GraphicsState state)
    {
        var bounds = SkiaConversions.ToSkMatrix(matrix).MapRect(Inflate(path.Bounds, state.LineWidth * Math.Max(1, state.MiterLimit)));
        _canvas.SetMatrix(SKMatrix.Identity);
        _layerPaint.Reset();
        _layerPaint.BlendMode = ElementBlend(state.BlendMode);
        _layerPaint.Color = WhiteWithAlpha(state.StrokeAlpha);
        SaveLayer(bounds, _layerPaint);
    }

    /// <summary>Strokes a path with a mesh shading: the stroke's outline is filled with the mesh.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="pattern">The mesh pattern.</param>
    /// <param name="scale">The matrix's average scale.</param>
    /// <param name="state">The graphics state.</param>
    private void StrokeMesh(SKPath path, Matrix3x2 matrix, PdfPatternPaint pattern, float scale, ref GraphicsState state)
    {
        ConfigureStroke(ref state);
        _paint.StrokeWidth = Math.Max(state.LineWidth, 1 / Math.Max(scale, MinScale));
        using var dash = CreateDash(state.Dash, state.DashPhase, scale);
        _paint.PathEffect = dash;
        using var builder = new SKPathBuilder();
        if (!_paint.GetFillPath(path, builder))
        {
            return;
        }

        using var outline = builder.Detach();
        FillMesh(outline, matrix, pattern, state.StrokeAlpha, state.BlendMode);
    }

    /// <summary>Fills a shape with a mesh shading pattern.</summary>
    /// <param name="path">The shape.</param>
    /// <param name="matrix">The matrix from the shape's space to the page.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    private void FillMesh(SKPath path, Matrix3x2 matrix, PdfPatternPaint pattern, float alpha, PdfBlendMode blend)
    {
        var mesh = pattern.Shading!.GetMesh(MatrixScale(pattern.Matrix));
        if (mesh is null)
        {
            return;
        }

        _ = _canvas.Save();
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(matrix));
        _canvas.ClipPath(path, SKClipOperation.Intersect, true);
        _canvas.SetMatrix(SkiaConversions.ToSkMatrix(pattern.Matrix));
        DrawMesh(mesh, alpha, blend);
        _canvas.Restore();
    }

    /// <summary>Draws the triangles of a mesh with the current canvas matrix.</summary>
    /// <param name="mesh">The mesh.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    private void DrawMesh(ShadingMesh mesh, float alpha, PdfBlendMode blend)
    {
        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.Color = WhiteWithAlpha(alpha);
        _paint.BlendMode = ElementBlend(blend);
        _paint.Shader = mesh.Ramp;
        foreach (var chunk in mesh.Chunks)
        {
            _canvas.DrawVertices(chunk, SKBlendMode.Dst, _paint);
        }
    }

    /// <summary>Paints a shading over the whole clip with the current canvas matrix.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="alpha">The alpha.</param>
    /// <param name="blend">The blend mode.</param>
    /// <param name="scale">The page units one unit of shading space spans, which sets how finely patch meshes are cut.</param>
    private void DrawShadingPaint(PdfShading shading, float alpha, PdfBlendMode blend, float scale)
    {
        if (shading.IsMesh)
        {
            if (shading.GetMesh(scale) is { } mesh)
            {
                DrawMesh(mesh, alpha, blend);
            }

            return;
        }

        if (shading.GetShader() is not { } shader)
        {
            return;
        }

        _paint.Reset();
        _paint.IsAntialias = true;
        _paint.Shader = shader;
        _paint.Color = WhiteWithAlpha(alpha);
        _paint.BlendMode = ElementBlend(blend);
        _canvas.DrawPaint(_paint);
    }
}
