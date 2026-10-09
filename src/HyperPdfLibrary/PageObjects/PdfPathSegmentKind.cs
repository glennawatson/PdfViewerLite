// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>The kinds of path segment.</summary>
public enum PdfPathSegmentKind
{
    /// <summary>No segment.</summary>
    None = 0,

    /// <summary>Starts a subpath at the first point.</summary>
    MoveTo = 1,

    /// <summary>A line to the first point.</summary>
    LineTo = 2,

    /// <summary>A cubic curve with two control points and an end point; <c>v</c> and <c>y</c> curves are written out in full.</summary>
    CurveTo = 3,

    /// <summary>A rectangle: the first point is its corner and the second is its width and height.</summary>
    Rectangle = 4,

    /// <summary>Closes the subpath.</summary>
    Close = 5,
}
