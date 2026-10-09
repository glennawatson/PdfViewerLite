// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Rendering;

/// <summary>What recording a page's annotations needs.</summary>
/// <param name="Cache">The document's caches.</param>
/// <param name="Interpreter">The interpreter drawing the appearances.</param>
/// <param name="Page">The page.</param>
/// <param name="Printing">Whether the page is rendered for printing.</param>
[DebuggerDisplay("AnnotationPass: page {Page.Index} printing {Printing}")]
internal sealed record AnnotationPass(PdfRenderCache Cache, ContentInterpreter Interpreter, PdfPage Page, bool Printing);
