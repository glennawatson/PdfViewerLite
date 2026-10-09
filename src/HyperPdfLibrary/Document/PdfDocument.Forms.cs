// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Document;

/// <content>The interactive form.</content>
public sealed partial class PdfDocument
{
    /// <summary>The interactive form, created on first use.</summary>
    private PdfForm? _form;

    /// <summary>Gets the document's interactive form (AcroForm). It is usable whether or not the document has one.</summary>
    public PdfForm Form => Volatile.Read(ref _form) ?? CreateForm();

    /// <summary>Creates the form unless another thread already did.</summary>
    /// <returns>The form every caller shares.</returns>
    private PdfForm CreateForm()
    {
        PdfForm created = new(this);
        return Interlocked.CompareExchange(ref _form, created, null) ?? created;
    }
}
