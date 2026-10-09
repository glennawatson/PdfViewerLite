// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>Decides font style facts shared by the font kinds.</summary>
internal static class FontStyle
{
    /// <summary>Determines whether a font is bold: ForceBold, a weight of 600 or more, or a bold style in its name.</summary>
    /// <param name="flags">The descriptor flags.</param>
    /// <param name="weight">The descriptor weight, or zero.</param>
    /// <param name="baseFont">The base font name.</param>
    /// <returns><see langword="true"/> when bold.</returns>
    internal static bool IsBold(FontFlags flags, int weight, string baseFont) =>
        (flags & FontFlags.ForceBold) != 0
        || weight >= ParsedFontName.SemiBoldWeight
        || ParsedFontName.Parse(baseFont).Weight >= ParsedFontName.SemiBoldWeight;
}
