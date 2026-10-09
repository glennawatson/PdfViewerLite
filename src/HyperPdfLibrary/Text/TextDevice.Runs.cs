// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <content>Grouping glyphs into runs.</content>
internal sealed partial class TextDevice
{
    /// <summary>The glyph-space units in one text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>How far, in run units, a glyph may sit off the run's baseline or behind the pen and still continue it.</summary>
    private const float ContinueTolerance = 0.01F;

    /// <summary>The gap between glyphs, in run units, below which no kerning is recorded.</summary>
    private const float KerningTolerance = 0.001F;

    /// <summary>The largest difference between matrix entries of glyphs in one run.</summary>
    private const float MatrixTolerance = 0.0001F;

    /// <summary>The first text render mode that strokes.</summary>
    private const int StrokeMode = 1;

    /// <summary>The fill-and-stroke text render mode.</summary>
    private const int FillStrokeMode = 2;

    /// <summary>The stroke-and-clip text render mode.</summary>
    private const int StrokeClipMode = 5;

    /// <summary>The fill, stroke and clip text render mode.</summary>
    private const int FillStrokeClipMode = 6;

    /// <summary>Half, for the stroke half-width that widens stroked runs.</summary>
    private const float Half = 0.5F;

    /// <summary>Whether a run is open.</summary>
    private bool _open;

    /// <summary>The open run's font.</summary>
    private PdfFont? _font;

    /// <summary>The open run's font size.</summary>
    private float _fontSize;

    /// <summary>The open run's render mode.</summary>
    private int _mode;

    /// <summary>The open run's character spacing.</summary>
    private float _charSpacing;

    /// <summary>The open run's word spacing.</summary>
    private float _wordSpacing;

    /// <summary>The open run's line width, which widens stroked runs.</summary>
    private float _lineWidth;

    /// <summary>Whether the open run writes vertically.</summary>
    private bool _vertical;

    /// <summary>The open run's matrix.</summary>
    private Matrix3x2 _matrix;

    /// <summary>The inverse of the open run's matrix.</summary>
    private Matrix3x2 _inverse;

    /// <summary>The open run's marks.</summary>
    private TextMarks _runMarks = TextMarks.None;

    /// <summary>The open run's first glyph.</summary>
    private int _first;

    /// <summary>Where the next glyph of the open run would start without kerning.</summary>
    private float _nextAlong;

    /// <summary>The open run's glyph bounds in run units: left.</summary>
    private float _minX;

    /// <summary>The open run's glyph bounds in run units: bottom.</summary>
    private float _minY;

    /// <summary>The open run's glyph bounds in run units: right.</summary>
    private float _maxX;

    /// <summary>The open run's glyph bounds in run units: top.</summary>
    private float _maxY;

    /// <summary>Determines whether a stroking render mode widens a run's bounds.</summary>
    /// <param name="mode">The render mode.</param>
    /// <returns><see langword="true"/> for the stroking modes.</returns>
    private static bool IsStrokeMode(int mode) => mode is StrokeMode or FillStrokeMode or StrokeClipMode or FillStrokeClipMode;

    /// <summary>Determines whether two matrices share their scale, rotation and skew.</summary>
    /// <param name="first">The first matrix.</param>
    /// <param name="second">The second matrix.</param>
    /// <returns><see langword="true"/> when the linear parts match.</returns>
    private static bool SameLinear(Matrix3x2 first, Matrix3x2 second) =>
        MathF.Abs(first.M11 - second.M11) <= MatrixTolerance
        && MathF.Abs(first.M12 - second.M12) <= MatrixTolerance
        && MathF.Abs(first.M21 - second.M21) <= MatrixTolerance
        && MathF.Abs(first.M22 - second.M22) <= MatrixTolerance;

    /// <summary>Gets a glyph's outline bounds in thousandths of text space, or an empty box for a blank glyph.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <returns>The bounds.</returns>
    private static PdfRectangle GlyphBox(PdfFont font, int code)
    {
        if (font.GetOutline(code) is not { } outline)
        {
            return default;
        }

        var bounds = outline.Bounds;
        if (bounds.IsEmpty)
        {
            return default;
        }

        var toUnits = font.FontMatrix * Matrix3x2.CreateScale(GlyphUnits);
        return TextGeometry.TransformRect(toUnits, new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }

    /// <summary>Gets a glyph's advance in thousandths, falling back to its box width as PDFium does.</summary>
    /// <param name="width">The advance in text space units.</param>
    /// <param name="box">The glyph box in thousandths.</param>
    /// <returns>The width.</returns>
    private static int WidthUnits(float width, in PdfRectangle box)
    {
        var units = (int)MathF.Round(width * GlyphUnits);
        return units > 0 ? units : Math.Max((int)(box.Right - box.Left), 0);
    }

    /// <summary>Determines whether a glyph continues the open run.</summary>
    /// <param name="font">The glyph's font.</param>
    /// <param name="fontSize">The glyph's font size.</param>
    /// <param name="matrix">The glyph's run matrix.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="true"/> when the glyph belongs to the open run.</returns>
    private bool Continues(PdfFont font, float fontSize, Matrix3x2 matrix, ref GraphicsState state)
    {
        var sameRun = _open && SameState(font, fontSize, ref state) && SameLinear(matrix, _matrix);
        if (!sameRun || _hooked)
        {
            return sameRun && !_newObject;
        }

        // Without text object starts from the interpreter, a glyph on the run's baseline at or past the pen continues the
        // run, and any gap is kerning, as it is inside a TJ array.
        var local = Vector2.Transform(matrix.Translation, _inverse);
        var along = _vertical ? local.Y : local.X;
        var across = _vertical ? local.X : local.Y;
        var behind = _vertical ? along - _nextAlong : _nextAlong - along;
        return MathF.Abs(across) <= ContinueTolerance && behind <= ContinueTolerance;
    }

    /// <summary>Determines whether the text state matches the open run's.</summary>
    /// <param name="font">The font.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="true"/> when nothing that shapes a run changed.</returns>
    private bool SameState(PdfFont font, float fontSize, ref GraphicsState state) =>
        ReferenceEquals(font, _font)
        && ReferenceEquals(_currentMarks, _runMarks)
        && TextGeometry.Same(fontSize, _fontSize)
        && state.RenderMode == _mode
        && TextGeometry.Same(state.CharacterSpacing, _charSpacing)
        && TextGeometry.Same(state.WordSpacing, _wordSpacing);

    /// <summary>Starts a run at a glyph.</summary>
    /// <param name="font">The font.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="matrix">The run matrix.</param>
    /// <param name="state">The graphics state.</param>
    private void OpenRun(PdfFont font, float fontSize, Matrix3x2 matrix, ref GraphicsState state)
    {
        _open = true;
        _font = font;
        _fontSize = fontSize;
        _mode = state.RenderMode;
        _charSpacing = state.CharacterSpacing;
        _wordSpacing = state.WordSpacing;
        _lineWidth = state.LineWidth;
        _vertical = font.IsVertical;
        _matrix = matrix;
        _inverse = TextGeometry.Invert(matrix);
        _runMarks = CurrentMarks();
        _first = Glyphs.Count;
        _nextAlong = 0;
        _minX = float.MaxValue;
        _minY = float.MaxValue;
        _maxX = float.MinValue;
        _maxY = float.MinValue;
    }

    /// <summary>Adds the open run to the run list.</summary>
    private void CloseRun()
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        var count = Glyphs.Count - _first;
        if (count == 0)
        {
            return;
        }

        var rect = TextGeometry.TransformRect(_matrix, new(_minX, _minY, _maxX, _maxY));
        if (IsStrokeMode(_mode))
        {
            var half = _lineWidth * Half;
            rect = new(rect.Left - half, rect.Bottom - half, rect.Right + half, rect.Top + half);
        }

        Runs.Add(new(_font!, _fontSize, _mode, _charSpacing, _matrix, rect, _first, count, _runMarks));
    }

    /// <summary>Adds a glyph to the open run.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="origin">The glyph's text origin in user space.</param>
    /// <param name="state">The graphics state.</param>
    private void AddGlyph(in GlyphEvent glyph, Vector2 origin, ref GraphicsState state)
    {
        var font = glyph.Font;
        var code = glyph.Code;
        var fontSize = _fontSize;
        var along = 0F;
        if (Glyphs.Count > _first)
        {
            var local = Vector2.Transform(origin, _inverse);
            along = _vertical ? local.Y : local.X;
            SetPreviousKerning(along - _nextAlong, fontSize);
        }

        var box = GlyphBox(font, code);
        var width = font.GetWidth(code);
        var charWidth = width * fontSize;
        var verticalOrigin = Vector2.Zero;
        if (_vertical)
        {
            font.GetVerticalMetrics(code, out var advance, out var originX, out var originY);
            charWidth = advance * fontSize;
            verticalOrigin = new(originX * GlyphUnits, originY * GlyphUnits);
        }

        var spacing = state.CharacterSpacing + (glyph.IsWordSpace ? state.WordSpacing : 0);
        _nextAlong = along + charWidth + spacing;
        Extend(along, box, verticalOrigin, fontSize);
        Glyphs.Add(new(code, Unicode.Count, glyph.Unicode.Length, along, 0, WidthUnits(width, box), charWidth, box, verticalOrigin));
        Unicode.AddRange(glyph.Unicode);
    }

    /// <summary>Records the kerning before a glyph on the previous glyph, as a TJ adjustment.</summary>
    /// <param name="gap">How far the glyph starts past where the previous glyph's advance ended.</param>
    /// <param name="fontSize">The font size.</param>
    private void SetPreviousKerning(float gap, float fontSize)
    {
        if (MathF.Abs(gap) <= KerningTolerance || fontSize == 0)
        {
            return;
        }

        var last = Glyphs.Count - 1;
        Glyphs[last] = Glyphs[last] with { Kerning = -gap * GlyphUnits / fontSize };
    }

    /// <summary>Grows the open run's bounds by a glyph, as PDFium's CalcPositionData does.</summary>
    /// <param name="along">The glyph position.</param>
    /// <param name="box">The glyph box in thousandths.</param>
    /// <param name="verticalOrigin">The vertical origin in thousandths.</param>
    /// <param name="fontSize">The font size.</param>
    private void Extend(float along, in PdfRectangle box, Vector2 verticalOrigin, float fontSize)
    {
        var scale = fontSize / GlyphUnits;
        float x1;
        float x2;
        float y1;
        float y2;
        if (_vertical)
        {
            x1 = (box.Left - verticalOrigin.X) * scale;
            x2 = (box.Right - verticalOrigin.X) * scale;
            y1 = along + ((box.Top - verticalOrigin.Y) * scale);
            y2 = along + ((box.Bottom - verticalOrigin.Y) * scale);
        }
        else
        {
            x1 = along + (box.Left * scale);
            x2 = along + (box.Right * scale);
            y1 = box.Bottom * scale;
            y2 = box.Top * scale;
        }

        _minX = MathF.Min(_minX, MathF.Min(x1, x2));
        _maxX = MathF.Max(_maxX, MathF.Max(x1, x2));
        _minY = MathF.Min(_minY, MathF.Min(y1, y2));
        _maxY = MathF.Max(_maxY, MathF.Max(y1, y2));
    }
}
