// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>The pop-up window of a markup annotation.</summary>
/// <param name="Page">The zero based page index.</param>
/// <param name="Rect">The window's rectangle.</param>
/// <param name="IsOpen">Whether the window shows.</param>
[DebuggerDisplay("PdfInterchangePopup: page {Page} open={IsOpen}")]
public sealed record PdfInterchangePopup(int Page, PdfRectangle Rect, bool IsOpen);
