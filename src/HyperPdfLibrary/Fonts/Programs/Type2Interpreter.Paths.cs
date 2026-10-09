// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <content>Path construction operators.</content>
internal ref partial struct Type2Interpreter
{
    /// <summary>The rmoveto operator.</summary>
    private const int RMoveTo = 21;

    /// <summary>The hmoveto operator.</summary>
    private const int HMoveTo = 22;

    /// <summary>The rlineto operator.</summary>
    private const int RLineTo = 5;

    /// <summary>The hlineto operator.</summary>
    private const int HLineTo = 6;

    /// <summary>The rrcurveto operator.</summary>
    private const int RRCurveTo = 8;

    /// <summary>The rcurveline operator.</summary>
    private const int RCurveLine = 24;

    /// <summary>The rlinecurve operator.</summary>
    private const int RLineCurve = 25;

    /// <summary>The vvcurveto operator.</summary>
    private const int VVCurveTo = 26;

    /// <summary>The hhcurveto operator.</summary>
    private const int HHCurveTo = 27;

    /// <summary>The hvcurveto operator.</summary>
    private const int HVCurveTo = 31;

    /// <summary>The arguments of one curve.</summary>
    private const int CurveArguments = 6;

    /// <summary>The arguments of one short curve in the h/v forms.</summary>
    private const int ShortCurveArguments = 4;

    /// <summary>The arguments left for the final curve of an alternating run that carries one extra coordinate.</summary>
    private const int LastCurveArguments = 5;

    /// <summary>The arguments of one line segment.</summary>
    private const int LineArguments = 2;

    /// <summary>The index of the third curve argument.</summary>
    private const int Third = 2;

    /// <summary>The index of the fourth curve argument.</summary>
    private const int Fourth = 3;

    /// <summary>The index of the fifth curve argument.</summary>
    private const int Fifth = 4;

    /// <summary>The index of the sixth curve argument.</summary>
    private const int Sixth = 5;

    /// <summary>Closes the open contour, if any.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void ClosePath<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        if (!_open)
        {
            return;
        }

        sink.Close();
        _open = false;
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

    /// <summary>Draws a line to an absolute point.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="sink">The sink.</param>
    private void LineTo<TSink>(float x, float y, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        EnsureOpen(ref sink);
        _x = x;
        _y = y;
        sink.LineTo(x, y);
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

    /// <summary>Draws a curve from six relative arguments starting at a stack index.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="i">The first argument.</param>
    /// <param name="sink">The sink.</param>
    private void RelativeCurve<TSink>(int i, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var x1 = _x + _stack[i];
        var y1 = _y + _stack[i + 1];
        var x2 = x1 + _stack[i + Third];
        var y2 = y1 + _stack[i + Fourth];
        CurveTo(x1, y1, x2, y2, x2 + _stack[i + Fifth], y2 + _stack[i + Sixth], ref sink);
    }

    /// <summary>Runs rmoveto, hmoveto or vmoveto.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    private void Move<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        TakeWidth(_count > (op == RMoveTo ? LineArguments : 1));
        var dx = op is RMoveTo or HMoveTo ? _stack[0] : 0;
        var dy = op switch
        {
            RMoveTo => _stack[1],
            HMoveTo => 0,
            _ => _stack[0],
        };

        ClosePath(ref sink);
        _x += dx;
        _y += dy;
        sink.MoveTo(_x, _y);
        _open = true;
        _count = 0;
    }

    /// <summary>Runs rlineto, hlineto or vlineto.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    private void Lines<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        if (op == RLineTo)
        {
            for (var i = 0; i + 1 < _count; i += LineArguments)
            {
                LineTo(_x + _stack[i], _y + _stack[i + 1], ref sink);
            }
        }
        else
        {
            var horizontal = op == HLineTo;
            for (var i = 0; i < _count; i++)
            {
                if (horizontal)
                {
                    LineTo(_x + _stack[i], _y, ref sink);
                }
                else
                {
                    LineTo(_x, _y + _stack[i], ref sink);
                }

                horizontal = !horizontal;
            }
        }

        _count = 0;
    }

    /// <summary>Runs one of the curve operators.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    private void Curves<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        switch (op)
        {
            case RRCurveTo:
            {
                for (var i = 0; i + CurveArguments <= _count; i += CurveArguments)
                {
                    RelativeCurve(i, ref sink);
                }

                break;
            }

            case RCurveLine:
            {
                CurveLine(ref sink);
                break;
            }

            case RLineCurve:
            {
                LineCurve(ref sink);
                break;
            }

            case VVCurveTo or HHCurveTo:
            {
                SameDirectionCurves(op == HHCurveTo, ref sink);
                break;
            }

            default:
            {
                AlternatingCurves(op == HVCurveTo, ref sink);
                break;
            }
        }

        _count = 0;
    }

    /// <summary>Runs rcurveline: curves, then one line.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void CurveLine<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var i = 0;
        for (; _count - i >= CurveArguments + LineArguments; i += CurveArguments)
        {
            RelativeCurve(i, ref sink);
        }

        if (_count - i >= LineArguments)
        {
            LineTo(_x + _stack[i], _y + _stack[i + 1], ref sink);
        }
    }

    /// <summary>Runs rlinecurve: lines, then one curve.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void LineCurve<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var i = 0;
        for (; _count - i >= CurveArguments + LineArguments; i += LineArguments)
        {
            LineTo(_x + _stack[i], _y + _stack[i + 1], ref sink);
        }

        if (_count - i >= CurveArguments)
        {
            RelativeCurve(i, ref sink);
        }
    }

    /// <summary>Runs hhcurveto or vvcurveto, whose curves start and end in the same direction.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="horizontal">Whether the curves are horizontal.</param>
    /// <param name="sink">The sink.</param>
    private void SameDirectionCurves<TSink>(bool horizontal, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var i = _count & 1;
        var across = i == 1 ? _stack[0] : 0;
        for (; i + ShortCurveArguments <= _count; i += ShortCurveArguments)
        {
            float x1;
            float y1;
            if (horizontal)
            {
                x1 = _x + _stack[i];
                y1 = _y + across;
            }
            else
            {
                x1 = _x + across;
                y1 = _y + _stack[i];
            }

            var x2 = x1 + _stack[i + 1];
            var y2 = y1 + _stack[i + Third];
            var end = _stack[i + Fourth];
            CurveTo(x1, y1, x2, y2, horizontal ? x2 + end : x2, horizontal ? y2 : y2 + end, ref sink);
            across = 0;
        }
    }

    /// <summary>Runs hvcurveto or vhcurveto, whose curves alternate between horizontal and vertical tangents.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="horizontal">Whether the first curve starts horizontally.</param>
    /// <param name="sink">The sink.</param>
    private void AlternatingCurves<TSink>(bool horizontal, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var i = 0;
        while (i + ShortCurveArguments <= _count)
        {
            var last = _count - i == LastCurveArguments;
            var extra = last ? _stack[i + Fifth] : 0;
            var x1 = horizontal ? _x + _stack[i] : _x;
            var y1 = horizontal ? _y : _y + _stack[i];
            var x2 = x1 + _stack[i + 1];
            var y2 = y1 + _stack[i + Third];
            var x3 = horizontal ? x2 + extra : x2 + _stack[i + Fourth];
            var y3 = horizontal ? y2 + _stack[i + Fourth] : y2 + extra;
            CurveTo(x1, y1, x2, y2, x3, y3, ref sink);
            i += last ? LastCurveArguments : ShortCurveArguments;
            horizontal = !horizontal;
        }
    }
}
