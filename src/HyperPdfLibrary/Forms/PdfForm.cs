// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Shared state for a document's interactive form. Focused helpers read and edit its fields; edits are serialised and
/// replace copies of dictionaries in the document's object store.
/// </summary>
[DebuggerDisplay("PdfForm: {Document.PageCount} pages")]
public sealed class PdfForm
{
    /// <summary>Initializes a new instance of the <see cref="PdfForm"/> class.</summary>
    /// <param name="document">The document.</param>
    internal PdfForm(PdfDocument document) => Document = document;

    /// <summary>Gets the document.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the lock serialising edits.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>Gets the appearance streams this form may replace in place, guarded by <see cref="Gate"/>.</summary>
    internal HashSet<int> Drawn { get; } = [];
}
