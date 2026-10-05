// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Compatibility.Tests;

/// <summary>The corpus manifest, <c>tests/corpus/corpus.json</c>.</summary>
/// <param name="Comment">What the file is.</param>
/// <param name="Documents">The documents.</param>
[DebuggerDisplay("RealWorldPdfManifest: {Documents.Count} documents")]
internal sealed record RealWorldPdfManifest(string Comment, IReadOnlyList<RealWorldPdf> Documents);
