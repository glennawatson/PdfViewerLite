// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Redaction;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <content>Redaction marks: redact annotations made, listed and moved like other annotations.</content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>Moves a redaction mark: its areas are scaled into the new rectangle and its preview is drawn again.</summary>
    /// <param name="annotation">The annotation, a copy that is put back afterwards.</param>
    /// <param name="map">The move.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    private bool MoveRedaction(PdfDictionary annotation, in RectangleMap map)
    {
        var regions = PdfRedactions.GetRegions(annotation);
        if (regions.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < regions.Length; i++)
        {
            var low = map.Apply(new Vector2(regions[i].Left, regions[i].Bottom));
            var high = map.Apply(new Vector2(regions[i].Right, regions[i].Top));
            regions[i] = PdfRectangle.FromCorners(low.X, low.Y, high.X, high.Y);
        }

        PdfRedactions.SetRegions(_store, annotation, regions);
        return true;
    }

    /// <summary>Marks areas for redaction.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="areas">The areas in page space.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private int AddRedaction(int pageIndex, ReadOnlySpan<PageRect> areas)
    {
        if (areas.IsEmpty)
        {
            return -1;
        }

        lock (_gate)
        {
            if (GetPage(pageIndex) is not { } page)
            {
                return -1;
            }

            var regions = new PdfRectangle[areas.Length];
            for (var i = 0; i < regions.Length; i++)
            {
                regions[i] = ToUserRectangle(page, areas[i]);
            }

            var annotation = PdfRedactions.CreateAnnotation(_store, regions, PdfRedactionAppearance.Black);
            return Add(pageIndex, page, annotation, null, string.Empty, []);
        }
    }

    /// <summary>Marks the area between two corners of a drag for redaction.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="start">Where the drag started.</param>
    /// <param name="end">Where it ended.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddRedaction(int pageIndex, PagePoint start, PagePoint end) =>
        AddRedaction(pageIndex, [PageRect.FromEdges(start.X, start.Y, end.X, end.Y)]);
}
