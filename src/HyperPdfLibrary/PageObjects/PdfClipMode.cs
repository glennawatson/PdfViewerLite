// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>How a path object also clips what is painted after it.</summary>
public enum PdfClipMode
{
    /// <summary>The path does not clip.</summary>
    None = 0,

    /// <summary>The path clips by the non-zero winding rule (<c>W</c>).</summary>
    NonZero = 1,

    /// <summary>The path clips by the even-odd rule (<c>W*</c>).</summary>
    EvenOdd = 2,
}
