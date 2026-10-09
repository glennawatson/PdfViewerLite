// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <content>Path construction operators.</content>
internal ref partial struct Type1Interpreter
{
    /// <summary>The vmoveto operator.</summary>
    private const int VMoveTo = 4;

    /// <summary>The rlineto operator.</summary>
    private const int RLineTo = 5;

    /// <summary>The hlineto operator.</summary>
    private const int HLineTo = 6;

    /// <summary>The rrcurveto operator.</summary>
    private const int RRCurveTo = 8;

    /// <summary>The rmoveto operator.</summary>
    private const int RMoveTo = 21;

    /// <summary>The vhcurveto operator.</summary>
    private const int VHCurveTo = 30;

    /// <summary>The index of the third argument.</summary>
    private const int Third = 2;

    /// <summary>The index of the fourth argument.</summary>
    private const int Fourth = 3;

    /// <summary>The index of the fifth argument.</summary>
    private const int Fifth = 4;

    /// <summary>The index of the sixth argument.</summary>
    private const int Sixth = 5;

    /// <summary>Closes the open contour, if any.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void ClosePath<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var wasOpen = _open;
        _open = false;
        if (wasOpen)
        {
            sink.Close();
        }
    }

    /// <summary>Starts a contour at the current point when none is open.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void EnsureOpen<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var wasOpen = _open;
        _open = true;
        if (!wasOpen)
        {
            sink.MoveTo(_x, _y);
        }
    }

    /// <summary>Runs rmoveto, hmoveto or vmoveto; during a flex the point is collected instead.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    private void Move<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var dx = op switch
        {
            RMoveTo => Pick(0),
            VMoveTo => 0,
            _ => Pick(0),
        };

        var dy = op switch
        {
            RMoveTo => Pick(1),
            VMoveTo => Pick(0),
            _ => 0,
        };

        _x += dx;
        _y += dy;
        if (_flexing)
        {
            AddFlexPoint();
            return;
        }

        ClosePath(ref sink);
        sink.MoveTo(_x, _y);
        _open = true;
    }

    /// <summary>Records the current point as a flex point.</summary>
    private void AddFlexPoint()
    {
        if (_flexCount >= FlexPoints)
        {
            return;
        }

        _flex[_flexCount * Axes] = _x;
        _flex[(_flexCount * Axes) + 1] = _y;
        _flexCount++;
    }

    /// <summary>Runs rlineto, hlineto or vlineto.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    private void Line<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        EnsureOpen(ref sink);
        if (op == RLineTo)
        {
            _x += Pick(0);
            _y += Pick(1);
        }
        else if (op == HLineTo)
        {
            _x += Pick(0);
        }
        else
        {
            _y += Pick(0);
        }

        sink.LineTo(_x, _y);
    }

    /// <summary>Runs rrcurveto, vhcurveto or hvcurveto.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    private void Curve<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        float x1;
        float y1;
        float x2;
        float y2;
        if (op == RRCurveTo)
        {
            x1 = _x + Pick(0);
            y1 = _y + Pick(1);
            x2 = x1 + Pick(Third);
            y2 = y1 + Pick(Fourth);
            CurveTo(x1, y1, x2, y2, x2 + Pick(Fifth), y2 + Pick(Sixth), ref sink);
            return;
        }

        var vertical = op == VHCurveTo;
        x1 = vertical ? _x : _x + Pick(0);
        y1 = vertical ? _y + Pick(0) : _y;
        x2 = x1 + Pick(1);
        y2 = y1 + Pick(Third);
        var end = Pick(Fourth);
        CurveTo(x1, y1, x2, y2, vertical ? x2 + end : x2, vertical ? y2 : y2 + end, ref sink);
    }

    /// <summary>Draws a cubic curve through absolute points.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="x1">The first control x.</param>
    /// <param name="y1">The first control y.</param>
    /// <param name="x2">The second control x.</param>
    /// <param name="y2">The second control y.</param>
    /// <param name="x3">The end x.</param>
    /// <param name="y3">The end y.</param>
    /// <param name="sink">The sink.</param>
    private void CurveTo<TSink>(float x1, float y1, float x2, float y2, float x3, float y3, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        EnsureOpen(ref sink);
        _x = x3;
        _y = y3;
        sink.CubicTo(x1, y1, x2, y2, x3, y3);
    }
}
