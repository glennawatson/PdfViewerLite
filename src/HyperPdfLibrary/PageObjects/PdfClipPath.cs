// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>One clipping path in force when an object is painted.</summary>
/// <param name="Segments">The path, in the coordinates the content stream wrote.</param>
/// <param name="Matrix">The matrix from those coordinates to user space when the clip was set.</param>
/// <param name="EvenOdd">Whether the path clips by the even-odd rule.</param>
/// <param name="Bounds">The path's bounds in user space.</param>
[DebuggerDisplay("PdfClipPath: {Bounds}")]
public sealed record PdfClipPath(PdfPathSegment[] Segments, Matrix3x2 Matrix, bool EvenOdd, PdfRectangle Bounds);
