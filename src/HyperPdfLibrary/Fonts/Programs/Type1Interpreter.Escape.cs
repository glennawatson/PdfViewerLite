// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <content>Two-byte operators: metrics, seac, div and the OtherSubrs protocol.</content>
internal ref partial struct Type1Interpreter
{
    /// <summary>The seac operator.</summary>
    private const int OpSeac = 6;

    /// <summary>The sbw operator.</summary>
    private const int OpSbw = 7;

    /// <summary>The div operator.</summary>
    private const int OpDiv = 12;

    /// <summary>The callothersubr operator.</summary>
    private const int OpCallOtherSubr = 16;

    /// <summary>The pop operator.</summary>
    private const int OpPop = 17;

    /// <summary>The setcurrentpoint operator.</summary>
    private const int OpSetCurrentPoint = 33;

    /// <summary>The OtherSubr that ends a flex.</summary>
    private const int FlexEnd = 0;

    /// <summary>The OtherSubr that starts a flex.</summary>
    private const int FlexStart = 1;

    /// <summary>The arguments of seac.</summary>
    private const int SeacArguments = 5;

    /// <summary>The arguments of OtherSubr 0: the flex depth and the end point.</summary>
    private const int FlexEndArguments = 3;

    /// <summary>The argument of OtherSubr 0 that holds the end x.</summary>
    private const int FlexEndX = 1;

    /// <summary>The argument of OtherSubr 0 that holds the end y.</summary>
    private const int FlexEndY = 2;

    /// <summary>The index of the second curve's first point among the flex points.</summary>
    private const int SecondCurve = 4;

    /// <summary>Runs a two-byte operator.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The second byte.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>How the caller continues.</returns>
    private Flow RunEscape<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        switch (op)
        {
            case OpSeac:
            {
                Seac();
                return Flow.End;
            }

            case OpSbw:
            {
                SetMetrics(Pick(0), Pick(1), Pick(Third));
                return Flow.Continue;
            }

            case OpDiv:
            {
                Divide();
                return Flow.Continue;
            }

            case OpCallOtherSubr:
            {
                CallOtherSubr(ref sink);
                return Flow.Continue;
            }

            default:
            {
                RunStackEscape(op);
                return Flow.Continue;
            }
        }
    }

    /// <summary>Runs pop, setcurrentpoint and the operators that only clear the stack.</summary>
    /// <param name="op">The second byte.</param>
    private void RunStackEscape(int op)
    {
        if (op == OpPop)
        {
            var value = _resultCount > 0 ? _results[_resultCount - 1] : 0;
            _resultCount = Math.Max(_resultCount - 1, 0);
            Push(value);
            return;
        }

        if (op == OpSetCurrentPoint && _count >= Axes)
        {
            _x = _originX + _stack[_count - Axes];
            _y = _originY + _stack[_count - 1];
        }

        // dotsection, vstem3, hstem3 and reserved operators only clear the stack.
        _count = 0;
    }

    /// <summary>Records the parts of an accented character.</summary>
    private void Seac()
    {
        if (_count < SeacArguments)
        {
            _count = 0;
            return;
        }

        var bottom = _count - SeacArguments;
        var accentSideBearing = _stack[bottom];
        Outcome = Outcome with
        {
            HasSeac = true,
            AccentX = _stack[bottom + 1] - accentSideBearing + _sideBearing,
            AccentY = _stack[bottom + Third],
            BaseCode = (int)_stack[bottom + Fourth],
            AccentCode = (int)_stack[bottom + Fifth],
        };

        _count = 0;
    }

    /// <summary>Runs div, which replaces the top two values with their quotient.</summary>
    private void Divide()
    {
        if (_count < Axes)
        {
            return;
        }

        var divisor = _stack[_count - 1];
        _count--;
        _stack[_count - 1] = divisor is 0F ? 0F : _stack[_count - 1] / divisor;
    }

    /// <summary>
    /// Runs callothersubr. OtherSubrs 0 and 1 end and start a flex; the others return their arguments to pop, which
    /// gives the standard hint replacement (OtherSubr 3) its subroutine number back.
    /// </summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void CallOtherSubr<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        if (_count < Axes)
        {
            _count = 0;
            return;
        }

        var number = (int)_stack[_count - 1];
        var arguments = Math.Clamp((int)_stack[_count - Axes], 0, _count - Axes);
        _count -= Axes;
        var bottom = _count - arguments;
        _resultCount = 0;
        if (number == FlexEnd && arguments == FlexEndArguments)
        {
            EndFlex(ref sink);
            PushResult(_stack[bottom + FlexEndY]);
            PushResult(_stack[bottom + FlexEndX]);
        }
        else
        {
            for (var i = bottom; i < _count; i++)
            {
                PushResult(_stack[i]);
            }
        }

        if (number == FlexStart)
        {
            _flexing = true;
            _flexCount = 0;
        }

        _count = bottom;
    }

    /// <summary>Draws the two curves of a finished flex from the six points after the reference point.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void EndFlex<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        _flexing = false;
        if (_flexCount < FlexPoints)
        {
            return;
        }

        CurveTo(_flex[Axes], _flex[Axes + 1], _flex[Axes * Third], _flex[(Axes * Third) + 1], _flex[Axes * Fourth], _flex[(Axes * Fourth) + 1], ref sink);
        const int Second = SecondCurve * Axes;
        CurveTo(_flex[Second], _flex[Second + 1], _flex[Second + Axes], _flex[Second + Axes + 1], _flex[Second + (Axes * Third)], _flex[Second + (Axes * Third) + 1], ref sink);
    }

    /// <summary>Pushes a value onto the PostScript result stack.</summary>
    /// <param name="value">The value.</param>
    private void PushResult(float value)
    {
        if (_resultCount >= _results.Length)
        {
            return;
        }

        _results[_resultCount] = value;
        _resultCount++;
    }
}
