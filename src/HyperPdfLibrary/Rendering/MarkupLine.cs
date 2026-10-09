// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Adds and strokes the line a text markup annotation draws for one quadrilateral.</summary>
/// <param name="builder">The content.</param>
/// <param name="rect">The quadrilateral's rectangle.</param>
internal delegate void MarkupLine(ref PdfContentBuilder builder, PdfRectangle rect);
