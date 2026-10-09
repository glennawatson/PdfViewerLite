// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>Decides which failures while reading a font's parts mean the part is damaged rather than a bug.</summary>
internal static class FontLoadErrors
{
    /// <summary>Determines whether an exception comes from damaged font data.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the part should be treated as missing.</returns>
    internal static bool IsDamagedData(Exception exception) => exception is InvalidDataException or PdfException or ArgumentException
        or InvalidOperationException or NotSupportedException or FormatException or IndexOutOfRangeException or OverflowException;
}
