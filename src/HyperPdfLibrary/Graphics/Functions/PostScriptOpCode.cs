// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>An instruction of a compiled PostScript calculator program. Operators follow the control codes in table order.</summary>
internal enum PostScriptOpCode
{
    /// <summary>Push the instruction's operand.</summary>
    Push = 0,

    /// <summary>Jump to the instruction's target.</summary>
    Jump = 1,

    /// <summary>Pop a boolean and jump to the target when it is false.</summary>
    JumpIfFalse = 2,

    /// <summary>The <c>abs</c> operator.</summary>
    Abs = 3,

    /// <summary>The <c>add</c> operator.</summary>
    Add = 4,

    /// <summary>The <c>atan</c> operator.</summary>
    Atan = 5,

    /// <summary>The <c>ceiling</c> operator.</summary>
    Ceiling = 6,

    /// <summary>The <c>cos</c> operator.</summary>
    Cos = 7,

    /// <summary>The <c>cvi</c> operator.</summary>
    Cvi = 8,

    /// <summary>The <c>cvr</c> operator.</summary>
    Cvr = 9,

    /// <summary>The <c>div</c> operator.</summary>
    Div = 10,

    /// <summary>The <c>exp</c> operator.</summary>
    Exp = 11,

    /// <summary>The <c>floor</c> operator.</summary>
    Floor = 12,

    /// <summary>The <c>idiv</c> operator.</summary>
    Idiv = 13,

    /// <summary>The <c>ln</c> operator.</summary>
    Ln = 14,

    /// <summary>The <c>log</c> operator.</summary>
    Log = 15,

    /// <summary>The <c>mod</c> operator.</summary>
    Mod = 16,

    /// <summary>The <c>mul</c> operator.</summary>
    Mul = 17,

    /// <summary>The <c>neg</c> operator.</summary>
    Neg = 18,

    /// <summary>The <c>round</c> operator.</summary>
    Round = 19,

    /// <summary>The <c>sin</c> operator.</summary>
    Sin = 20,

    /// <summary>The <c>sqrt</c> operator.</summary>
    Sqrt = 21,

    /// <summary>The <c>sub</c> operator.</summary>
    Sub = 22,

    /// <summary>The <c>truncate</c> operator.</summary>
    Truncate = 23,

    /// <summary>The <c>and</c> operator.</summary>
    And = 24,

    /// <summary>The <c>bitshift</c> operator.</summary>
    Bitshift = 25,

    /// <summary>The <c>eq</c> operator.</summary>
    Eq = 26,

    /// <summary>The <c>false</c> operator.</summary>
    False = 27,

    /// <summary>The <c>ge</c> operator.</summary>
    Ge = 28,

    /// <summary>The <c>gt</c> operator.</summary>
    Gt = 29,

    /// <summary>The <c>le</c> operator.</summary>
    Le = 30,

    /// <summary>The <c>lt</c> operator.</summary>
    Lt = 31,

    /// <summary>The <c>ne</c> operator.</summary>
    Ne = 32,

    /// <summary>The <c>not</c> operator.</summary>
    Not = 33,

    /// <summary>The <c>or</c> operator.</summary>
    Or = 34,

    /// <summary>The <c>true</c> operator.</summary>
    True = 35,

    /// <summary>The <c>xor</c> operator.</summary>
    Xor = 36,

    /// <summary>The <c>copy</c> operator.</summary>
    Copy = 37,

    /// <summary>The <c>dup</c> operator.</summary>
    Dup = 38,

    /// <summary>The <c>exch</c> operator.</summary>
    Exch = 39,

    /// <summary>The <c>index</c> operator.</summary>
    Index = 40,

    /// <summary>The <c>pop</c> operator.</summary>
    Pop = 41,

    /// <summary>The <c>roll</c> operator.</summary>
    Roll = 42,
}
