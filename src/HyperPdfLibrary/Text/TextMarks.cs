// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>The marked content a text run is in, as PDFium reads it for /ActualText.</summary>
/// <param name="Count">The number of open marked content levels.</param>
/// <param name="Innermost">The property list of the innermost level, or null.</param>
/// <param name="LastProperties">The innermost property list that is present, or null.</param>
/// <param name="HasActualText">Whether any level's property list has an /ActualText string.</param>
/// <param name="ActualText">The innermost /ActualText string present, or null.</param>
/// <param name="LastActualText">The /ActualText of <see cref="LastProperties"/>; empty when it has none.</param>
[DebuggerDisplay("TextMarks: {Count} levels")]
internal sealed record TextMarks(
    int Count,
    PdfDictionary? Innermost,
    PdfDictionary? LastProperties,
    bool HasActualText,
    string? ActualText,
    string LastActualText)
{
    /// <summary>Gets the marks of text outside any marked content.</summary>
    internal static TextMarks None { get; } = new(0, null, null, false, null, string.Empty);
}
