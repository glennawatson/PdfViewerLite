// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>A growing bounding box in PDF user space (y up), built while points are converted.</summary>
[DebuggerDisplay("PdfBounds: {_left},{_bottom} – {_right},{_top}")]
internal record struct PdfBounds
{
    /// <summary>The left edge.</summary>
    private double _left;

    /// <summary>The bottom edge.</summary>
    private double _bottom;

    /// <summary>The right edge.</summary>
    private double _right;

    /// <summary>The top edge.</summary>
    private double _top;

    /// <summary>Whether a point has been added.</summary>
    private bool _any;

    /// <summary>Adds a PDF space point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    internal void Add(double x, double y)
    {
        if (!_any)
        {
            _left = x;
            _right = x;
            _bottom = y;
            _top = y;
            _any = true;
            return;
        }

        _left = Math.Min(_left, x);
        _right = Math.Max(_right, x);
        _bottom = Math.Min(_bottom, y);
        _top = Math.Max(_top, y);
    }

    /// <summary>Adds a page space point, converting it to PDF space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point.</param>
    internal void Add(PdfiumPage page, PagePoint point)
    {
        page.ToPdf(point, out var x, out var y);
        Add(x, y);
    }

    /// <summary>Gets the box as a PDFium rectangle, grown on every side by a margin.</summary>
    /// <param name="margin">The margin, for example half a line width.</param>
    /// <returns>The rectangle.</returns>
    internal readonly FsRectF ToRect(float margin) =>
        new((float)_left - margin, (float)_top + margin, (float)_right + margin, (float)_bottom - margin);
}
