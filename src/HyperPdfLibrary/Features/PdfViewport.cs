// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Features;

/// <summary>A viewport: a region of a page with its own measurement scale (<c>/VP</c>).</summary>
/// <param name="Bounds">The region in the page's user space (<c>/BBox</c>), or null.</param>
/// <param name="Name">The viewport's name, or null.</param>
/// <param name="Measure">The measurement data, or null.</param>
[DebuggerDisplay("PdfViewport: {Name}")]
public sealed record PdfViewport(PdfRectangle? Bounds, string? Name, PdfMeasure? Measure);
