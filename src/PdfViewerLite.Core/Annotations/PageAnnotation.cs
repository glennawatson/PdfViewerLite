// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Annotations;

/// <summary>An annotation on a page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation's index on the page; changes when annotations before it are removed.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Bounds">The bounds in page space (points, top-left origin).</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
/// <param name="Contents">The note text, or an empty string.</param>
/// <param name="Author">The author, or an empty string.</param>
[DebuggerDisplay("{Kind} on page {PageIndex}")]
public sealed record PageAnnotation(int PageIndex, int Index, AnnotationKind Kind, PageRect Bounds, uint Color, string Contents, string Author);
