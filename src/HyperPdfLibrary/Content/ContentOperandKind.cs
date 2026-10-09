// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Content;

/// <summary>The kind of a content stream operand.</summary>
public enum ContentOperandKind
{
    /// <summary>A missing operand, or null.</summary>
    None = 0,

    /// <summary>A number.</summary>
    Number = 1,

    /// <summary>A name.</summary>
    Name = 2,

    /// <summary>A literal string; the range is the body between the parentheses, still escaped.</summary>
    LiteralString = 3,

    /// <summary>A hexadecimal string; the range is the digits between the angle brackets.</summary>
    HexString = 4,

    /// <summary>An array; the range is the body between the brackets.</summary>
    Array = 5,

    /// <summary>A dictionary; the range covers the whole <c>&lt;&lt; ... &gt;&gt;</c>.</summary>
    Dictionary = 6,

    /// <summary>A boolean; the number is 1 for true.</summary>
    Boolean = 7,
}
