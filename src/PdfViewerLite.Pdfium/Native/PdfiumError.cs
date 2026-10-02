// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium <c>FPDF_ERR_*</c> codes.</summary>
internal enum PdfiumError
{
    /// <summary>No error.</summary>
    Success = 0,

    /// <summary>An unknown error.</summary>
    Unknown = 1,

    /// <summary>The file could not be found or opened.</summary>
    File = 2,

    /// <summary>The file is not a PDF or is corrupted.</summary>
    Format = 3,

    /// <summary>A password is required or incorrect.</summary>
    Password = 4,

    /// <summary>An unsupported security scheme.</summary>
    Security = 5,

    /// <summary>The page was not found or has content errors.</summary>
    Page = 6,
}
