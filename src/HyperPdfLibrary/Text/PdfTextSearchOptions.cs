// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>Text search options, with the values of PDFium's FPDF_MATCHCASE, FPDF_MATCHWHOLEWORD and FPDF_CONSECUTIVE.</summary>
[Flags]
public enum PdfTextSearchOptions
{
    /// <summary>Case-insensitive, partial word matching; each match starts after the previous one ends.</summary>
    None = 0,

    /// <summary>Match letter case.</summary>
    MatchCase = 1 << 0,

    /// <summary>Match whole words only.</summary>
    WholeWord = 1 << 1,

    /// <summary>Look for the next match from the character after the previous match's start, so matches may overlap.</summary>
    Consecutive = 1 << 2,
}
