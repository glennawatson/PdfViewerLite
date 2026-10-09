// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// A document's interactive form (AcroForm). Widgets are addressed by page and by their index in the page's
/// <c>/Annots</c> array. Reading never changes the document. Edits replace copies of the changed dictionaries in the
/// document's object store, so readers on other threads see either the old or the new dictionary, never a half change,
/// and the document saves them as an incremental update. JavaScript is never run.
/// </summary>
[DebuggerDisplay("PdfForm: HasForm={HasForm}")]
public sealed partial class PdfForm
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>Serialises edits.</summary>
    private readonly Lock _gate = new();

    /// <summary>The numbers of the appearance streams this form drew, which it may replace in place. Guarded by <see cref="_gate"/>.</summary>
    private readonly HashSet<int> _drawn = [];

    /// <summary>Initializes a new instance of the <see cref="PdfForm"/> class.</summary>
    /// <param name="document">The document.</param>
    internal PdfForm(PdfDocument document) => _document = document;

    /// <summary>Gets a value indicating whether the document has an AcroForm.</summary>
    public bool HasForm => PdfDocumentContent.HasAcroForm(_document);

    /// <summary>Gets a value indicating whether the form asks readers to rebuild every field's appearance (<c>/NeedAppearances</c>).</summary>
    public bool NeedAppearances => AcroForm?.GetBoolean(KnownName.NeedAppearances) ?? false;

    /// <summary>Gets the AcroForm dictionary, or <see langword="null"/>.</summary>
    private PdfDictionary? AcroForm => _document.Catalog.GetDictionary(KnownName.AcroForm);

    /// <summary>Gets the document's objects.</summary>
    private PdfObjectStore Store => _document.Objects;
}
