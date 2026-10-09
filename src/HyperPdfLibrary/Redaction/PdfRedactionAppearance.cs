// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Redaction;

/// <summary>How a redacted area looks once the redaction is applied: a fill colour and optional overlay text.</summary>
/// <param name="FillColor">The colour of the fill as 0xRRGGBB (<c>/IC</c>), or <see langword="null"/> to leave the area unpainted.</param>
/// <param name="OverlayText">The text drawn over the area (<c>/OverlayText</c>), or an empty string for none.</param>
/// <param name="Repeat">Whether the text repeats to fill the area (<c>/Repeat</c>).</param>
/// <param name="FontSize">The overlay text size in points; 0 fits the text to the height of the area.</param>
/// <param name="TextColor">The overlay text colour as 0xRRGGBB, or <see langword="null"/> to pick white or black to contrast with the fill.</param>
/// <param name="Alignment">How the text sits across the area (<c>/Q</c>).</param>
[DebuggerDisplay("PdfRedactionAppearance: fill {FillColor}, text '{OverlayText}'")]
public sealed record PdfRedactionAppearance(
    uint? FillColor,
    string OverlayText,
    bool Repeat,
    float FontSize,
    uint? TextColor,
    PdfRedactionAlignment Alignment)
{
    /// <summary>Gets a black bar with no text, the usual look of a redaction.</summary>
    public static PdfRedactionAppearance Black { get; } = new(0, string.Empty, false, 0, null, PdfRedactionAlignment.Left);
}
