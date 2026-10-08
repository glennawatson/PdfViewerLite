// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Text;

namespace PdfViewerLite.Core.Annotations;

/// <summary>
/// Writes formatted text boxes and reads them back for editing. Text is kept as a standard free text annotation with
/// its own drawn appearance, so other readers show it the same way. Safe to call from any thread.
/// </summary>
public interface ITextBoxEditor
{
    /// <summary>Writes a text box.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the box.</param>
    /// <param name="wrapWidth">The width lines wrap at, in points, or 0 to break lines only where the text does.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="format">How the text looks.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddTextBox(int pageIndex, PagePoint location, float wrapWidth, string text, TextFormat format);

    /// <summary>Reads a text box's text, look and place, from this viewer or another program.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The content, or <see langword="null"/> when the annotation is not editable text.</returns>
    TextBoxContent? GetTextBox(int pageIndex, int index);
}
