// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Runs Type 1 charstrings (Adobe Type 1 Font Format, chapter 6) and sends the outline to a sink. Flex through
/// OtherSubrs 0 to 2 becomes two curves; hint replacement through OtherSubr 3 is honoured; hints are ignored.
/// </summary>
internal ref partial struct Type1Interpreter
{
    /// <summary>The scratch space the interpreter needs: the stack, the PostScript result stack and the flex points.</summary>
    internal const int ScratchSize = CharstringLimits.StackDepth + PostScriptDepth + (FlexPoints * Axes);

    /// <summary>The depth of the PostScript result stack used by callothersubr and pop.</summary>
    private const int PostScriptDepth = 24;

    /// <summary>The number of points a flex collects: a reference point and six curve points.</summary>
    private const int FlexPoints = 7;

    /// <summary>The coordinates per point.</summary>
    private const int Axes = 2;

    /// <summary>The first byte that starts an operand.</summary>
    private const int FirstOperandByte = 32;

    /// <summary>The last single-byte integer.</summary>
    private const int SmallIntEnd = 246;

    /// <summary>The bias of single-byte integers.</summary>
    private const int SmallIntBias = 139;

    /// <summary>The first positive two-byte integer prefix.</summary>
    private const int PositiveStart = 247;

    /// <summary>The first negative two-byte integer prefix.</summary>
    private const int NegativeStart = 251;

    /// <summary>The prefix of a 32-bit integer.</summary>
    private const int LongInt = 255;

    /// <summary>The bias of two-byte integers.</summary>
    private const int TwoByteBias = 108;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The charstring data the subroutine ranges refer to.</summary>
    private readonly ReadOnlySpan<byte> _charData;

    /// <summary>The subroutines.</summary>
    private readonly ReadOnlySpan<TableRange> _subrs;

    /// <summary>The argument stack.</summary>
    private readonly Span<float> _stack;

    /// <summary>The PostScript result stack.</summary>
    private readonly Span<float> _results;

    /// <summary>The flex points, as x and y pairs.</summary>
    private readonly Span<float> _flex;

    /// <summary>Whether the run stops once the width is known.</summary>
    private readonly bool _widthOnly;

    /// <summary>The x of the glyph origin, used to place an accent.</summary>
    private readonly float _originX;

    /// <summary>The y of the glyph origin.</summary>
    private readonly float _originY;

    /// <summary>The number of arguments on the stack.</summary>
    private int _count;

    /// <summary>The number of values on the PostScript result stack.</summary>
    private int _resultCount;

    /// <summary>The number of flex points collected.</summary>
    private int _flexCount;

    /// <summary>Whether a flex is being collected.</summary>
    private bool _flexing;

    /// <summary>The current x.</summary>
    private float _x;

    /// <summary>The current y.</summary>
    private float _y;

    /// <summary>The left side bearing from hsbw or sbw.</summary>
    private float _sideBearing;

    /// <summary>Whether a contour is open.</summary>
    private bool _open;

    /// <summary>Whether the run has finished.</summary>
    private bool _done;

    /// <summary>Initializes a new instance of the <see cref="Type1Interpreter"/> struct.</summary>
    /// <param name="charData">The charstring data.</param>
    /// <param name="subrs">The subroutines.</param>
    /// <param name="origin">The glyph origin, non-zero for the accent of an accented character.</param>
    /// <param name="widthOnly">Whether to stop once the width is known.</param>
    /// <param name="scratch">Space for the stacks and flex points, at least <see cref="ScratchSize"/> values.</param>
    internal Type1Interpreter(ReadOnlySpan<byte> charData, ReadOnlySpan<TableRange> subrs, GlyphOrigin origin, bool widthOnly, Span<float> scratch)
    {
        _charData = charData;
        _subrs = subrs;
        _stack = scratch[..CharstringLimits.StackDepth];
        _results = scratch.Slice(CharstringLimits.StackDepth, PostScriptDepth);
        _flex = scratch.Slice(CharstringLimits.StackDepth + PostScriptDepth, FlexPoints * Axes);
        (_originX, _originY) = origin;
        (_x, _y) = origin;
        _widthOnly = widthOnly;
    }

    /// <summary>The outcome of one operator.</summary>
    private enum Flow
    {
        /// <summary>Carry on with the next byte.</summary>
        Continue = 0,

        /// <summary>Return from the current subroutine.</summary>
        Return = 1,

        /// <summary>Stop the whole run.</summary>
        End = 2,
    }

    /// <summary>The operator families of the one-byte operators.</summary>
    private enum Family
    {
        /// <summary>A reserved operator, which clears the stack.</summary>
        Reserved = 0,

        /// <summary>A hint operator, ignored.</summary>
        Hint = 1,

        /// <summary>A moveto operator.</summary>
        Move = 2,

        /// <summary>A line operator.</summary>
        Line = 3,

        /// <summary>A curve operator.</summary>
        Curve = 4,

        /// <summary>The closepath operator.</summary>
        ClosePath = 5,

        /// <summary>A subroutine call.</summary>
        CallSubr = 6,

        /// <summary>A subroutine return.</summary>
        Return = 7,

        /// <summary>A two-byte operator.</summary>
        Escape = 8,

        /// <summary>The hsbw operator.</summary>
        Hsbw = 9,

        /// <summary>The end of the charstring.</summary>
        EndChar = 10,
    }

    /// <summary>Gets the width and any accented-character request the run found.</summary>
    internal CharstringOutcome Outcome { get; private set; }

    /// <summary>Gets the family of each one-byte operator.</summary>
    private static ReadOnlySpan<byte> Families =>
    [
        0x00, 0x01, 0x00, 0x01, 0x02, 0x03, 0x03, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x04,
    ];

    /// <summary>Runs a charstring.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="charstring">The decrypted charstring.</param>
    /// <param name="sink">The sink.</param>
    internal void Run<TSink>(ReadOnlySpan<byte> charstring, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        _ = Execute(charstring, 0, ref sink);
        ClosePath(ref sink);
    }

    /// <summary>Executes a charstring or subroutine.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="code">The bytes.</param>
    /// <param name="depth">The subroutine depth.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>How the caller continues.</returns>
    private Flow Execute<TSink>(ReadOnlySpan<byte> code, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var position = 0;
        while (position < code.Length)
        {
            int b = code[position];
            if (b >= FirstOperandByte)
            {
                position = ReadNumber(code, position);
                continue;
            }

            position++;
            var family = (Family)Families[b];
            var flow = family <= Family.ClosePath
                ? RunPathOperator(family, b, ref sink)
                : RunFlowOperator(family, code, ref position, depth, ref sink);
            if (flow != Flow.Continue || _done)
            {
                return _done ? Flow.End : flow;
            }
        }

        return Flow.Continue;
    }

    /// <summary>Runs a hint or path operator.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="family">The operator family.</param>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>Always <see cref="Flow.Continue"/>.</returns>
    private Flow RunPathOperator<TSink>(Family family, int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        switch (family)
        {
            case Family.Move:
            {
                Move(op, ref sink);
                break;
            }

            case Family.Line:
            {
                Line(op, ref sink);
                break;
            }

            case Family.Curve:
            {
                Curve(op, ref sink);
                break;
            }

            case Family.ClosePath:
            {
                ClosePath(ref sink);
                break;
            }

            default:
            {
                break;
            }
        }

        _count = 0;
        return Flow.Continue;
    }

    /// <summary>Runs a subroutine, return, metrics, endchar or two-byte operator.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="family">The operator family.</param>
    /// <param name="code">The bytes.</param>
    /// <param name="position">The read position, after the operator.</param>
    /// <param name="depth">The subroutine depth.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>How the caller continues.</returns>
    private Flow RunFlowOperator<TSink>(Family family, ReadOnlySpan<byte> code, ref int position, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        switch (family)
        {
            case Family.CallSubr:
            {
                return CallSubroutine(depth, ref sink);
            }

            case Family.Return:
            {
                return Flow.Return;
            }

            case Family.Hsbw:
            {
                SetMetrics(Pick(0), 0, Pick(1));
                return Flow.Continue;
            }

            case Family.EndChar:
            {
                _count = 0;
                return Flow.End;
            }

            default:
            {
                var op = FontBytes.U8(code, position);
                position++;
                return RunEscape(op, ref sink);
            }
        }
    }

    /// <summary>Calls the subroutine numbered by the top of the stack.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="depth">The current depth.</param>
    /// <param name="sink">The sink.</param>
    /// <returns><see cref="Flow.End"/> when the subroutine ended the charstring.</returns>
    private Flow CallSubroutine<TSink>(int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        if (_count == 0 || depth >= CharstringLimits.MaxSubrDepth)
        {
            _count = 0;
            return Flow.Continue;
        }

        _count--;
        var index = (int)_stack[_count];
        if ((uint)index >= (uint)_subrs.Length)
        {
            return Flow.Continue;
        }

        var subr = _subrs[index].Of(_charData);
        return Execute(subr, depth + 1, ref sink) == Flow.End ? Flow.End : Flow.Continue;
    }

    /// <summary>Sets the side bearing and width from hsbw or sbw.</summary>
    /// <param name="sideBearingX">The left side bearing x.</param>
    /// <param name="sideBearingY">The left side bearing y.</param>
    /// <param name="width">The advance width.</param>
    private void SetMetrics(float sideBearingX, float sideBearingY, float width)
    {
        _sideBearing = sideBearingX;
        _x = _originX + sideBearingX;
        _y = _originY + sideBearingY;
        Outcome = Outcome with { Width = width };
        _done = _widthOnly;
        _count = 0;
    }

    /// <summary>Gets a stack value, or zero when missing.</summary>
    /// <param name="index">The index from the bottom of the stack.</param>
    /// <returns>The value.</returns>
    private readonly float Pick(int index) => (uint)index < (uint)_count ? _stack[index] : 0;

    /// <summary>Reads an operand and pushes it.</summary>
    /// <param name="code">The bytes.</param>
    /// <param name="position">The operand's first byte.</param>
    /// <returns>The position after the operand.</returns>
    private int ReadNumber(ReadOnlySpan<byte> code, int position)
    {
        int b = code[position];
        float value;
        int size;
        if (b <= SmallIntEnd)
        {
            value = b - SmallIntBias;
            size = 1;
        }
        else if (b == LongInt)
        {
            value = (int)FontBytes.U32(code, position + 1);
            size = 1 + FontBytes.U32Size;
        }
        else
        {
            var next = FontBytes.U8(code, position + 1);
            value = b < NegativeStart ? ((b - PositiveStart) << ByteBits) + next + TwoByteBias : -((b - NegativeStart) << ByteBits) - next - TwoByteBias;
            size = FontBytes.U16Size;
        }

        Push(value);
        return position + size;
    }

    /// <summary>Pushes a value, dropping it when the stack is full.</summary>
    /// <param name="value">The value.</param>
    private void Push(float value)
    {
        if (_count >= _stack.Length)
        {
            return;
        }

        _stack[_count] = value;
        _count++;
    }
}
