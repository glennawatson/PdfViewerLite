// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.App.Tests;

/// <summary>Engines available to app tests, including the PDFium parity reference.</summary>
public enum TestEngineChoice
{
    /// <summary>The engine shipped with the app.</summary>
    HyperPdf = 0,

    /// <summary>The native reference engine used by parity tests.</summary>
    Pdfium = 1,
}
