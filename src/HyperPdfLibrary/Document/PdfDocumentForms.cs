// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and edits document forms.</summary>
public static class PdfDocumentForms
{
    /// <summary>Gets the document's interactive form (AcroForm). It is usable whether or not the document has one.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The lazily read form.</returns>
    public static PdfForm GetForm(PdfDocument document) => Volatile.Read(ref document.State.Form) ?? PdfDocumentForms.CreateForm(document);

    /// <summary>Creates the form unless another thread already did.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The form every caller shares.</returns>
    private static PdfForm CreateForm(PdfDocument document)
    {
        PdfForm created = new(document);
        return Interlocked.CompareExchange(ref document.State.Form, created, null) ?? created;
    }
}
