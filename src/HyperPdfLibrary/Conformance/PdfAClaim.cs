// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A conformance a file claims in its XMP <c>pdfaid</c> properties. A claim is what the file says about itself;
/// nothing here checks that the file meets it.
/// </summary>
/// <param name="Part">The part of ISO 19005 (<c>pdfaid:part</c>): 1, 2, 3 or 4.</param>
/// <param name="Conformance">
/// The conformance level (<c>pdfaid:conformance</c>) in upper case: A, B or U for parts 1 to 3; E or F for part 4, where it
/// is optional. <see langword="null"/> when absent.
/// </param>
/// <param name="Revision">The revision year (<c>pdfaid:rev</c>), for example 2020 for part 4, or <see langword="null"/>.</param>
[DebuggerDisplay("PdfAClaim: {Label}")]
public sealed record PdfAClaim(int Part, string? Conformance, int? Revision)
{
    /// <summary>The first part of ISO 19005, based on PDF 1.4.</summary>
    internal const int Part1 = 1;

    /// <summary>The second part of ISO 19005, based on PDF 1.7.</summary>
    internal const int Part2 = 2;

    /// <summary>The third part of ISO 19005.</summary>
    internal const int Part3 = 3;

    /// <summary>The fourth part of ISO 19005, based on PDF 2.0.</summary>
    internal const int Part4 = 4;

    /// <summary>Gets the claim as people write it, for example "1B", "3U" or "4F"; part 4 without a level is "4".</summary>
    public string Label => string.Concat(Part.ToString(System.Globalization.CultureInfo.InvariantCulture), Conformance);

    /// <summary>
    /// Gets a value indicating whether the part and level are a pair the standards define: 1A, 1B, 2A, 2B, 2U, 3A, 3B, 3U,
    /// and part 4 with no level, E or F.
    /// </summary>
    public bool IsRecognised => Part switch
    {
        Part1 => HasLevelIn("AB"),
        Part2 or Part3 => HasLevelIn("ABU"),
        Part4 => Conformance is null || HasLevelIn("EF"),
        _ => false,
    };

    /// <summary>Gets a value indicating whether the part forbids LZW compression (parts 1 to 3).</summary>
    internal bool ForbidsLzw => Part is >= Part1 and <= Part3;

    /// <summary>Gets a value indicating whether the part forbids transparency (part 1).</summary>
    internal bool ForbidsTransparency => Part == Part1;

    /// <summary>Gets a value indicating whether the part forbids embedded files (part 1).</summary>
    internal bool ForbidsEmbeddedFiles => Part == Part1;

    /// <summary>Determines whether the level is one letter from a set.</summary>
    /// <param name="levels">The allowed letters.</param>
    /// <returns><see langword="true"/> when the level is one of them.</returns>
    private bool HasLevelIn(string levels) => Conformance is { Length: 1 } level && levels.Contains(level[0], StringComparison.Ordinal);
}
