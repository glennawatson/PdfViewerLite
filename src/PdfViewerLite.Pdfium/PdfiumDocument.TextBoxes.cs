// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;

namespace PdfViewerLite.Pdfium;

/// <summary>Formatted text boxes.</summary>
public sealed partial class PdfiumDocument
{
    /// <summary>Gets or sets the installed fonts text boxes may be written in; the computer's fonts unless set.</summary>
    public FontCatalog FontCatalog
    {
        get
        {
            using var scope = PdfiumLibrary.EnterScope();
            return _fonts.Catalog;
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            using var scope = PdfiumLibrary.EnterScope();
            _fonts.Catalog = value;
        }
    }

    /// <inheritdoc/>
    public int AddTextBox(int pageIndex, PagePoint location, float wrapWidth, string text, TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(format);
        using var scope = PdfiumLibrary.EnterScope();
        var index = EditablePage(pageIndex) is { } page
            ? PdfiumAnnotations.AddTextBox(_fonts, page, location, (Math.Max(wrapWidth, 0), text), format.Clamped(), Author)
            : -1;
        return Pending(Changed(pageIndex, index));
    }

    /// <inheritdoc/>
    public TextBoxContent? GetTextBox(int pageIndex, int index)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return EditablePage(pageIndex) is { } page ? PdfiumAnnotations.GetTextBox(page, index) : null;
    }

    /// <inheritdoc/>
    public float GetFirstBaseline(string text, TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(format);
        var clamped = format.Clamped();
        using var scope = PdfiumLibrary.EnterScope();
        return _fonts.Resolve(clamped, text) is { } font ? TextBoxLayout.FirstBaseline(clamped, font.Shaper) : float.NaN;
    }
}
