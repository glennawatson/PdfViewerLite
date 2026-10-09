// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>A link annotation: a clickable area and where it leads.</summary>
/// <param name="Bounds">The clickable area in user space.</param>
/// <param name="Action">Where it leads.</param>
[DebuggerDisplay("PdfLink: {Action.Kind} @ {Bounds}")]
public sealed record PdfLink(PdfRectangle Bounds, PdfAction Action);
