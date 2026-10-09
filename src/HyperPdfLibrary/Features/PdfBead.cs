// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Features;

/// <summary>A bead: one rectangle of an article thread.</summary>
/// <param name="PageIndex">The zero based page, or -1 when the page is not in the document.</param>
/// <param name="Bounds">The rectangle in the page's user space, or null when missing.</param>
[DebuggerDisplay("PdfBead: page {PageIndex}")]
public sealed record PdfBead(int PageIndex, PdfRectangle? Bounds);
