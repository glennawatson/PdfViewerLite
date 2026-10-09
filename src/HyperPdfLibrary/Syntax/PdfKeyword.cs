// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Syntax;

/// <summary>The keywords of the file structure.</summary>
internal enum PdfKeyword
{
    /// <summary>Any other keyword.</summary>
    Other = 0,

    /// <summary>The <c>true</c> keyword.</summary>
    True = 1,

    /// <summary>The <c>false</c> keyword.</summary>
    False = 2,

    /// <summary>The <c>null</c> keyword.</summary>
    Null = 3,

    /// <summary>The <c>R</c> of a reference.</summary>
    Reference = 4,

    /// <summary>The <c>obj</c> keyword.</summary>
    Obj = 5,

    /// <summary>The <c>endobj</c> keyword.</summary>
    EndObj = 6,

    /// <summary>The <c>stream</c> keyword.</summary>
    Stream = 7,

    /// <summary>The <c>endstream</c> keyword.</summary>
    EndStream = 8,

    /// <summary>The <c>xref</c> keyword.</summary>
    Xref = 9,

    /// <summary>The <c>trailer</c> keyword.</summary>
    Trailer = 10,

    /// <summary>The <c>startxref</c> keyword.</summary>
    StartXref = 11,
}
