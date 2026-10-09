// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Syntax;

/// <summary>The kind of a PDF token.</summary>
internal enum PdfTokenKind
{
    /// <summary>The end of the data.</summary>
    EndOfData = 0,

    /// <summary>An integer or real number.</summary>
    Number = 1,

    /// <summary>A name; the token is the bytes after the slash, still escaped.</summary>
    Name = 2,

    /// <summary>A literal string; the token is the bytes between the outer parentheses, still escaped.</summary>
    LiteralString = 3,

    /// <summary>A hexadecimal string; the token is the bytes between the angle brackets.</summary>
    HexString = 4,

    /// <summary>The start of an array.</summary>
    ArrayStart = 5,

    /// <summary>The end of an array.</summary>
    ArrayEnd = 6,

    /// <summary>The start of a dictionary.</summary>
    DictionaryStart = 7,

    /// <summary>The end of a dictionary.</summary>
    DictionaryEnd = 8,

    /// <summary>A keyword or operator, such as <c>obj</c>, <c>R</c> or <c>Tj</c>.</summary>
    Keyword = 9,

    /// <summary>An opening brace, used by PostScript calculator functions.</summary>
    BraceOpen = 10,

    /// <summary>A closing brace.</summary>
    BraceClose = 11,
}
