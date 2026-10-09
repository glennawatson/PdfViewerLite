// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Syntax;

/// <summary>Recognises file-structure keywords.</summary>
internal static class PdfKeywords
{
    /// <summary>Gets the <c>obj</c> keyword.</summary>
    internal static ReadOnlySpan<byte> Obj => "obj"u8;

    /// <summary>Gets the <c>endstream</c> keyword.</summary>
    internal static ReadOnlySpan<byte> EndStream => "endstream"u8;

    /// <summary>Gets the <c>startxref</c> keyword.</summary>
    internal static ReadOnlySpan<byte> StartXref => "startxref"u8;

    /// <summary>Gets the <c>trailer</c> keyword.</summary>
    internal static ReadOnlySpan<byte> Trailer => "trailer"u8;

    /// <summary>Classifies a keyword, checking its first byte before comparing the rest.</summary>
    /// <param name="lexeme">The keyword's bytes.</param>
    /// <returns>The keyword, or <see cref="PdfKeyword.Other"/>.</returns>
    internal static PdfKeyword Classify(ReadOnlySpan<byte> lexeme)
    {
        if (lexeme.IsEmpty)
        {
            return PdfKeyword.Other;
        }

        return lexeme[0] switch
        {
            (byte)'R' => lexeme.Length == 1 ? PdfKeyword.Reference : PdfKeyword.Other,
            (byte)'t' => Match(lexeme, "true"u8, PdfKeyword.True, "trailer"u8, PdfKeyword.Trailer),
            (byte)'f' => Match(lexeme, "false"u8, PdfKeyword.False),
            (byte)'n' => Match(lexeme, "null"u8, PdfKeyword.Null),
            (byte)'o' => Match(lexeme, "obj"u8, PdfKeyword.Obj),
            (byte)'e' => Match(lexeme, "endobj"u8, PdfKeyword.EndObj, "endstream"u8, PdfKeyword.EndStream),
            (byte)'s' => Match(lexeme, "stream"u8, PdfKeyword.Stream, "startxref"u8, PdfKeyword.StartXref),
            (byte)'x' => Match(lexeme, "xref"u8, PdfKeyword.Xref),
            _ => PdfKeyword.Other,
        };
    }

    /// <summary>Matches one keyword.</summary>
    /// <param name="lexeme">The keyword's bytes.</param>
    /// <param name="spelling">The keyword's spelling.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns>The keyword, or <see cref="PdfKeyword.Other"/>.</returns>
    private static PdfKeyword Match(ReadOnlySpan<byte> lexeme, ReadOnlySpan<byte> spelling, PdfKeyword keyword) =>
        lexeme.SequenceEqual(spelling) ? keyword : PdfKeyword.Other;

    /// <summary>Matches one of two keywords sharing a first byte.</summary>
    /// <param name="lexeme">The keyword's bytes.</param>
    /// <param name="first">The first spelling.</param>
    /// <param name="firstKeyword">The first keyword.</param>
    /// <param name="second">The second spelling.</param>
    /// <param name="secondKeyword">The second keyword.</param>
    /// <returns>The keyword, or <see cref="PdfKeyword.Other"/>.</returns>
    private static PdfKeyword Match(ReadOnlySpan<byte> lexeme, ReadOnlySpan<byte> first, PdfKeyword firstKeyword, ReadOnlySpan<byte> second, PdfKeyword secondKeyword)
    {
        if (lexeme.SequenceEqual(first))
        {
            return firstKeyword;
        }

        return lexeme.SequenceEqual(second) ? secondKeyword : PdfKeyword.Other;
    }
}
