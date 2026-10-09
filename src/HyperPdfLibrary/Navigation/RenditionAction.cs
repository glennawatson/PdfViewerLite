// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>Controls the playing of a rendition on a screen annotation.</summary>
/// <param name="Operation">The <c>/OP</c> value: 0 play, 1 stop, 2 pause, 3 resume, 4 play or resume; -1 when missing.</param>
/// <param name="Rendition">The rendition, or null.</param>
/// <param name="Annotation">The screen annotation dictionary, or null.</param>
/// <param name="Script">The JavaScript (<c>/JS</c>) to run, or null.</param>
[DebuggerDisplay("RenditionAction: operation {Operation}")]
public sealed record RenditionAction(int Operation, PdfRendition? Rendition, PdfDictionary? Annotation, string? Script);
