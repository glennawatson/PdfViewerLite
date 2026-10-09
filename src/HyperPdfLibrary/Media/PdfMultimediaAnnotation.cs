// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Media;

/// <summary>A multimedia annotation on a page; the member that matches <see cref="Kind"/> is set.</summary>
/// <param name="Kind">The annotation kind.</param>
/// <param name="PageIndex">The zero based page.</param>
/// <param name="Bounds">The annotation's <c>/Rect</c>, or null.</param>
/// <param name="Screen">The screen annotation data, for <see cref="PdfMultimediaKind.Screen"/>.</param>
/// <param name="Movie">The movie data, for <see cref="PdfMultimediaKind.Movie"/>.</param>
/// <param name="Sound">The sound, for <see cref="PdfMultimediaKind.Sound"/>; null when the annotation has none.</param>
/// <param name="RichMedia">The rich media data, for <see cref="PdfMultimediaKind.RichMedia"/>.</param>
/// <param name="ThreeD">The 3D data, for <see cref="PdfMultimediaKind.ThreeD"/>.</param>
[DebuggerDisplay("PdfMultimediaAnnotation: {Kind} on page {PageIndex}")]
public sealed record PdfMultimediaAnnotation(
    PdfMultimediaKind Kind,
    int PageIndex,
    PdfRectangle? Bounds,
    PdfScreenAnnotation? Screen,
    PdfMovieAnnotation? Movie,
    PdfSound? Sound,
    PdfRichMediaAnnotation? RichMedia,
    Pdf3DAnnotation? ThreeD);
