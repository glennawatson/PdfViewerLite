// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>A built-in simple font encoding.</summary>
public enum FontEncoding
{
    /// <summary>No encoding.</summary>
    None = 0,

    /// <summary>Adobe StandardEncoding.</summary>
    Standard = 1,

    /// <summary>WinAnsiEncoding, Windows code page 1252.</summary>
    WinAnsi = 2,

    /// <summary>The Mac OS standard Roman encoding.</summary>
    MacRoman = 3,

    /// <summary>The Mac OS expert encoding.</summary>
    MacExpert = 4,

    /// <summary>The PDF document text encoding.</summary>
    PdfDoc = 5,

    /// <summary>The built-in encoding of the Symbol font.</summary>
    Symbol = 6,

    /// <summary>The built-in encoding of the ZapfDingbats font.</summary>
    ZapfDingbats = 7,

    /// <summary>The CFF predefined Expert encoding.</summary>
    Expert = 8,
}
