// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <content>Two-byte operators: flex and arithmetic.</content>
internal ref partial struct Type2Interpreter
{
    /// <summary>The and operator.</summary>
    private const int OpAnd = 3;

    /// <summary>The or operator.</summary>
    private const int OpOr = 4;

    /// <summary>The not operator.</summary>
    private const int OpNot = 5;

    /// <summary>The abs operator.</summary>
    private const int OpAbs = 9;

    /// <summary>The add operator.</summary>
    private const int OpAdd = 10;

    /// <summary>The sub operator.</summary>
    private const int OpSub = 11;

    /// <summary>The neg operator.</summary>
    private const int OpNeg = 14;

    /// <summary>The eq operator.</summary>
    private const int OpEq = 15;

    /// <summary>The drop operator.</summary>
    private const int OpDrop = 18;

    /// <summary>The put operator.</summary>
    private const int OpPut = 20;

    /// <summary>The get operator.</summary>
    private const int OpGet = 21;

    /// <summary>The ifelse operator.</summary>
    private const int OpIfElse = 22;

    /// <summary>The random operator.</summary>
    private const int OpRandom = 23;

    /// <summary>The mul operator.</summary>
    private const int OpMul = 24;

    /// <summary>The dup operator.</summary>
    private const int OpDup = 27;

    /// <summary>The exch operator.</summary>
    private const int OpExch = 28;

    /// <summary>The roll operator.</summary>
    private const int OpRoll = 30;

    /// <summary>The hflex operator.</summary>
    private const int OpHFlex = 34;

    /// <summary>The flex operator.</summary>
    private const int OpFlex = 35;

    /// <summary>The hflex1 operator.</summary>
    private const int OpHFlex1 = 36;

    /// <summary>The flex1 operator.</summary>
    private const int OpFlex1 = 37;

    /// <summary>The value random returns; any value in (0, 1] is allowed, and a fixed one keeps output stable.</summary>
    private const float RandomValue = 0.5F;

    /// <summary>The arguments of ifelse.</summary>
    private const int IfElseArguments = 4;

    /// <summary>The arguments of hflex.</summary>
    private const int HFlexArguments = 7;

    /// <summary>The arguments of flex.</summary>
    private const int FlexArguments = 13;

    /// <summary>The arguments of hflex1.</summary>
    private const int HFlex1Arguments = 9;

    /// <summary>The arguments of flex1.</summary>
    private const int Flex1Arguments = 11;

    /// <summary>The index of the seventh argument.</summary>
    private const int Seventh = 6;

    /// <summary>The index of the eighth argument.</summary>
    private const int Eighth = 7;

    /// <summary>The index of the ninth argument.</summary>
    private const int Ninth = 8;

    /// <summary>The index of the tenth argument.</summary>
    private const int Tenth = 9;

    /// <summary>The index of the eleventh argument.</summary>
    private const int Eleventh = 10;

    /// <summary>The largest difference eq treats as equal: half the step of a 16.16 fixed-point number.</summary>
    private const float EqualTolerance = 1F / 131_072F;

    /// <summary>The kind of a two-byte operator.</summary>
    private enum EscapeKind
    {
        /// <summary>The dotsection operator or a reserved operator, which clears the stack.</summary>
        Clear = 0,

        /// <summary>An operator with one argument.</summary>
        Unary = 1,

        /// <summary>An operator with two arguments.</summary>
        Binary = 2,

        /// <summary>A stack or storage operator.</summary>
        Stack = 3,

        /// <summary>A flex operator.</summary>
        Flex = 4,
    }

    /// <summary>Gets the kind of each two-byte operator, indexed by its second byte.</summary>
    private static ReadOnlySpan<byte> EscapeKinds =>
    [
        0x00, 0x00, 0x00, 0x02, 0x02, 0x01, 0x00, 0x00, 0x00, 0x01, 0x02, 0x02, 0x02, 0x00, 0x01, 0x02,
        0x00, 0x00, 0x03, 0x00, 0x03, 0x03, 0x03, 0x03, 0x02, 0x00, 0x01, 0x03, 0x02, 0x03, 0x03, 0x00,
        0x00, 0x00, 0x04, 0x04, 0x04, 0x04,
    ];

    /// <summary>Computes add, sub, mul or div.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>The result; division by zero gives zero.</returns>
    private static float Calculate(int op, float a, float b) => op switch
    {
        OpAdd => a + b,
        OpSub => a - b,
        OpMul => a * b,
        _ => b is 0F ? 0F : a / b,
    };

    /// <summary>Computes the logical operators and eq, which give one for true and zero for false.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>The result.</returns>
    private static float CompareValues(int op, float a, float b)
    {
        var truth = op switch
        {
            OpAnd => a is not 0F && b is not 0F,
            OpOr => a is not 0F || b is not 0F,
            _ => MathF.Abs(a - b) <= EqualTolerance,
        };

        return truth ? 1F : 0F;
    }

    /// <summary>Runs a two-byte operator.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The second byte.</param>
    /// <param name="sink">The sink.</param>
    private void RunEscape<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var kind = (uint)op < (uint)EscapeKinds.Length ? (EscapeKind)EscapeKinds[op] : EscapeKind.Clear;
        switch (kind)
        {
            case EscapeKind.Unary:
            {
                RunUnary(op);
                break;
            }

            case EscapeKind.Binary:
            {
                RunBinary(op);
                break;
            }

            case EscapeKind.Stack:
            {
                RunStackOperator(op);
                break;
            }

            case EscapeKind.Flex:
            {
                Flex(op, ref sink);
                _count = 0;
                break;
            }

            default:
            {
                _count = 0;
                break;
            }
        }
    }

    /// <summary>Runs a one-argument operator.</summary>
    /// <param name="op">The operator.</param>
    private void RunUnary(int op)
    {
        if (_count == 0)
        {
            return;
        }

        var value = _stack[_count - 1];
        _stack[_count - 1] = op switch
        {
            OpAbs => MathF.Abs(value),
            OpNeg => -value,
            OpNot => value is 0F ? 1F : 0F,
            _ => MathF.Sqrt(MathF.Max(value, 0)),
        };
    }

    /// <summary>Runs a two-argument operator.</summary>
    /// <param name="op">The operator.</param>
    private void RunBinary(int op)
    {
        if (_count < LineArguments)
        {
            return;
        }

        var a = _stack[_count - LineArguments];
        var b = _stack[_count - 1];
        if (op == OpExch)
        {
            _stack[_count - LineArguments] = b;
            _stack[_count - 1] = a;
            return;
        }

        _count--;
        _stack[_count - 1] = op is OpAnd or OpOr or OpEq ? CompareValues(op, a, b) : Calculate(op, a, b);
    }

    /// <summary>Runs a stack or storage operator.</summary>
    /// <param name="op">The operator.</param>
    private void RunStackOperator(int op)
    {
        switch (op)
        {
            case OpDrop:
            {
                _count = Math.Max(_count - 1, 0);
                break;
            }

            case OpDup:
            {
                Push(_count > 0 ? _stack[_count - 1] : 0);
                break;
            }

            case OpRandom:
            {
                Push(RandomValue);
                break;
            }

            case OpPut or OpGet:
            {
                Store(op == OpPut);
                break;
            }

            case OpIfElse:
            {
                IfElse();
                break;
            }

            default:
            {
                Reorder(op == OpRoll);
                break;
            }
        }
    }

    /// <summary>Runs put or get on the transient array.</summary>
    /// <param name="put">Whether the operator is put.</param>
    private void Store(bool put)
    {
        if (_count < (put ? LineArguments : 1))
        {
            _count = 0;
            return;
        }

        var slot = (int)_stack[_count - 1];
        if (put)
        {
            if ((uint)slot < CharstringLimits.TransientSize)
            {
                _transient[slot] = _stack[_count - LineArguments];
            }

            _count -= LineArguments;
            return;
        }

        _stack[_count - 1] = (uint)slot < CharstringLimits.TransientSize ? _transient[slot] : 0;
    }

    /// <summary>Runs ifelse.</summary>
    private void IfElse()
    {
        if (_count < IfElseArguments)
        {
            _count = 0;
            return;
        }

        var bottom = _count - IfElseArguments;
        var choice = _stack[bottom + Third] <= _stack[bottom + Fourth] ? _stack[bottom] : _stack[bottom + 1];
        _count = bottom + 1;
        _stack[bottom] = choice;
    }

    /// <summary>Runs index or roll.</summary>
    /// <param name="roll">Whether the operator is roll.</param>
    private void Reorder(bool roll)
    {
        if (!roll)
        {
            if (_count > 0)
            {
                var back = Math.Max((int)_stack[_count - 1], 0);
                _stack[_count - 1] = _count - LineArguments >= back ? _stack[_count - LineArguments - back] : 0;
            }

            return;
        }

        if (_count < LineArguments)
        {
            _count = 0;
            return;
        }

        var shift = (int)_stack[_count - 1];
        var size = (int)_stack[_count - LineArguments];
        _count -= LineArguments;
        if (size <= 0 || size > _count)
        {
            return;
        }

        var window = _stack.Slice(_count - size, size);
        var amount = ((shift % size) + size) % size;
        window.Reverse();
        window[..amount].Reverse();
        window[amount..].Reverse();
    }

    /// <summary>Runs one of the flex operators, which draw two curves.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="op">The operator.</param>
    /// <param name="sink">The sink.</param>
    private void Flex<TSink>(int op, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var startY = _y;
        switch (op)
        {
            case OpFlex when _count >= FlexArguments:
            {
                RelativeCurve(0, ref sink);
                RelativeCurve(CurveArguments, ref sink);
                break;
            }

            case OpHFlex when _count >= HFlexArguments:
            {
                var x1 = _x + _stack[0];
                var x2 = x1 + _stack[1];
                var y2 = _y + _stack[Third];
                var x3 = x2 + _stack[Fourth];
                CurveTo(x1, _y, x2, y2, x3, y2, ref sink);
                var x4 = _x + _stack[Fifth];
                var x5 = x4 + _stack[Sixth];
                CurveTo(x4, y2, x5, startY, x5 + _stack[Seventh], startY, ref sink);
                break;
            }

            case OpHFlex1 when _count >= HFlex1Arguments:
            {
                HFlex1(startY, ref sink);
                break;
            }

            case OpFlex1 when _count >= Flex1Arguments:
            {
                Flex1(ref sink);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Runs hflex1.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="startY">The y at the start of the flex.</param>
    /// <param name="sink">The sink.</param>
    private void HFlex1<TSink>(float startY, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var x1 = _x + _stack[0];
        var y1 = _y + _stack[1];
        var x2 = x1 + _stack[Third];
        var y2 = y1 + _stack[Fourth];
        CurveTo(x1, y1, x2, y2, x2 + _stack[Fifth], y2, ref sink);
        var x4 = _x + _stack[Sixth];
        var x5 = x4 + _stack[Seventh];
        var y5 = _y + _stack[Eighth];
        CurveTo(x4, _y, x5, y5, x5 + _stack[Ninth], startY, ref sink);
    }

    /// <summary>Runs flex1, whose last point moves along the axis the flex travels most.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="sink">The sink.</param>
    private void Flex1<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var startX = _x;
        var startY = _y;
        var x1 = _x + _stack[0];
        var y1 = _y + _stack[1];
        var x2 = x1 + _stack[Third];
        var y2 = y1 + _stack[Fourth];
        var x3 = x2 + _stack[Fifth];
        var y3 = y2 + _stack[Sixth];
        CurveTo(x1, y1, x2, y2, x3, y3, ref sink);
        var x4 = x3 + _stack[Seventh];
        var y4 = y3 + _stack[Eighth];
        var x5 = x4 + _stack[Ninth];
        var y5 = y4 + _stack[Tenth];
        var last = _stack[Eleventh];
        var horizontal = MathF.Abs(x5 - startX) > MathF.Abs(y5 - startY);
        CurveTo(x4, y4, x5, y5, horizontal ? x5 + last : startX, horizontal ? startY : y5 + last, ref sink);
    }
}
