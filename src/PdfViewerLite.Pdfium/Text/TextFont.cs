// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;

namespace PdfViewerLite.Pdfium.Text;

/// <summary>The font text is written in: an installed font to embed, or a built in one, and the styles drawn in.</summary>
/// <param name="Shaper">Shapes the text.</param>
/// <param name="Program">The installed font to embed, or <see langword="null"/> for a built in font.</param>
/// <param name="Standard">The built in font, or <see langword="null"/> for an installed font.</param>
/// <param name="FakeBold">Whether bold is drawn by thickening the outlines.</param>
/// <param name="FakeItalic">Whether italic is drawn by slanting.</param>
[DebuggerDisplay("TextFont: {Family}")]
internal sealed record TextFont(ITextShaper Shaper, FontProgram? Program, StandardFontShaper? Standard, bool FakeBold, bool FakeItalic)
{
    /// <summary>Gets the family the text is written in.</summary>
    public string Family => Program?.Face.Family ?? Standard?.BaseFont ?? string.Empty;
}
