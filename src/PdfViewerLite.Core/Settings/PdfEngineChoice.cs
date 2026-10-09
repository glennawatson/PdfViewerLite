// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Settings;

/// <summary>Which engine opens PDF documents.</summary>
public enum PdfEngineChoice
{
    /// <summary>PDFium, the native engine; the default.</summary>
    Pdfium = 0,

    /// <summary>HyperPDF, the fully managed engine.</summary>
    HyperPdf = 1,
}
