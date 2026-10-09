// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>A place in a document: a page and, when given, a point in its user space.</summary>
/// <param name="PageIndex">The zero based page index; for a remote destination, the page in the other file.</param>
/// <param name="Left">The left coordinate in user space, when given.</param>
/// <param name="Top">The top coordinate in user space, when given.</param>
/// <param name="Zoom">The zoom factor, when given.</param>
[DebuggerDisplay("PdfDestination: page {PageIndex} ({Left}, {Top})")]
public readonly record struct PdfDestination(int PageIndex, float? Left, float? Top, float? Zoom);
