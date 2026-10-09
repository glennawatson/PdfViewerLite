// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.PageObjects;

/// <summary>A path that is stroked, filled or both. Paths that only clip are part of the clip chain, not objects.</summary>
[DebuggerDisplay("PdfPathObject: {Segments.Length} segments, {PaintMode}")]
public sealed class PdfPathObject : PdfPageObject
{
    /// <summary>Initializes a new instance of the <see cref="PdfPathObject"/> class.</summary>
    internal PdfPathObject()
    {
    }

    /// <inheritdoc/>
    public override PdfPageObjectKind Kind => PdfPageObjectKind.Path;

    /// <summary>Gets the segments, in the coordinates the content stream wrote.</summary>
    public PdfPathSegment[] Segments { get; internal init; } = [];

    /// <summary>Gets how the path is painted.</summary>
    public PdfPathPaintMode PaintMode { get; internal init; }

    /// <summary>Gets a value indicating whether the fill uses the even-odd rule.</summary>
    public bool EvenOddFill { get; internal init; }

    /// <summary>Gets a value indicating whether the painting operator closed the path first (<c>s</c>, <c>b</c>, <c>b*</c>).</summary>
    public bool ClosesPath { get; internal init; }

    /// <summary>Gets how the path also clips what is painted after it.</summary>
    public PdfClipMode Clip { get; internal init; }
}
