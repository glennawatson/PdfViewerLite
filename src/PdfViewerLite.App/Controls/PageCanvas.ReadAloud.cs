// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.App.Controls;

/// <summary>Starting Read Aloud from the right-click menu.</summary>
public sealed partial class PageCanvas
{
    /// <summary>How far from the pointer to look for the text to start reading from, in points.</summary>
    private const float ReadFromTolerance = 24F;

    /// <summary>Reads aloud from the sentence nearest a point, or the top of the page when there is no text near it.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    private static void ReadAloudFrom(DocumentTabViewModel tab, int page, PagePoint point)
    {
        var index = tab.TryGetDocument()?.GetCharacterIndexAt(page, point, ReadFromTolerance) ?? -1;
        tab.ReadAloud.StartAt(page, Math.Max(0, index));
    }

    /// <summary>Reads aloud from the start of the selection.</summary>
    /// <param name="tab">The tab.</param>
    private void ReadAloudFromSelection(DocumentTabViewModel tab)
    {
        if (!TryGetSelection(out var start, out _))
        {
            return;
        }

        ClearSelection();
        tab.ReadAloud.StartAt(start.Page, start.Char);
    }
}
