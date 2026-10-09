// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;

namespace PdfViewerLite.App.Services;

/// <summary>
/// Opens each document with the engine the settings choose at that moment, so a change of engine applies to the next
/// document opened. The <c>PDFVIEWERLITE_ENGINE</c> environment variable (<c>pdfium</c> or <c>hyperpdf</c>) overrides
/// the setting, for testing.
/// </summary>
[DebuggerDisplay("SelectableDocumentEngine: {Name}")]
public sealed class SelectableDocumentEngine : IDocumentEngine
{
    /// <summary>The environment variable that overrides the setting.</summary>
    internal const string OverrideVariable = "PDFVIEWERLITE_ENGINE";

    /// <summary>The PDFium engine.</summary>
    private readonly PdfiumEngine _pdfium = new();

    /// <summary>The HyperPDF engine.</summary>
    private readonly HyperPdfEngine _hyperPdf = new();

    /// <summary>Reads the engine the settings choose.</summary>
    private readonly Func<PdfEngineChoice> _choice;

    /// <summary>Reads the tint over fillable form fields that each document opens with.</summary>
    private readonly Func<FormHighlight> _highlight;

    /// <summary>Initializes a new instance of the <see cref="SelectableDocumentEngine"/> class that draws the default form field tint.</summary>
    /// <param name="choice">Reads the engine the settings choose.</param>
    public SelectableDocumentEngine(Func<PdfEngineChoice> choice)
        : this(choice, static () => FormHighlight.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SelectableDocumentEngine"/> class.</summary>
    /// <param name="choice">Reads the engine the settings choose.</param>
    /// <param name="highlight">Reads the tint over fillable form fields; each document opens with it, whichever engine opens it.</param>
    public SelectableDocumentEngine(Func<PdfEngineChoice> choice, Func<FormHighlight> highlight)
    {
        ArgumentNullException.ThrowIfNull(choice);
        ArgumentNullException.ThrowIfNull(highlight);
        _choice = choice;
        _highlight = highlight;
    }

    /// <inheritdoc/>
    public string Name => Current.Name;

    /// <summary>Gets the engine documents open with now.</summary>
    public IDocumentEngine Current => Resolve(_choice(), Environment.GetEnvironmentVariable(OverrideVariable)) == PdfEngineChoice.HyperPdf ? _hyperPdf : _pdfium;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanOpen(string path) => Current.CanOpen(path);

    /// <inheritdoc/>
    public IDocument Open(string path, string? password)
    {
        var document = Current.Open(path, password);
        if (document is IFormHighlight tinted)
        {
            tinted.Highlight = _highlight();
        }

        return document;
    }

    /// <summary>Works out the engine from the setting and the override.</summary>
    /// <param name="setting">The engine the settings choose.</param>
    /// <param name="variable">The override variable's value, if set.</param>
    /// <returns>The engine to use.</returns>
    internal static PdfEngineChoice Resolve(PdfEngineChoice setting, string? variable) => variable?.Trim().ToUpperInvariant() switch
    {
        "HYPERPDF" => PdfEngineChoice.HyperPdf,
        "PDFIUM" => PdfEngineChoice.Pdfium,
        _ => setting,
    };
}
