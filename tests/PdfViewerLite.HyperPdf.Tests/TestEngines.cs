// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>The engines the engine-parameterised suites run on.</summary>
public static class TestEngines
{
    /// <summary>Gets both engine names, for <c>[MethodDataSource]</c>.</summary>
    /// <returns>The names.</returns>
    public static IEnumerable<string> All() => [EngineDocument.Pdfium, EngineDocument.HyperPdf];
}
