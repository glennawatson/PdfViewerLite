// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Forms.Scripting;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Interactive forms, read and filled by the managed library. Field edits live in the managed objects with every other
/// edit, so drawing and saving both show them.
/// </content>
public sealed partial class HyperPdfDocument : IFormFiller, IFormScriptSource
{
    /// <inheritdoc/>
    public bool HasForm => !IsDisposed && HyperPdfLibrary.Document.PdfDocumentForms.GetForm(_document).HasForm;

    /// <summary>Gets a value indicating whether the managed store holds edits that are not saved.</summary>
    internal bool HasManagedEdits => !IsDisposed && _document.Objects.HasEdits;

    /// <inheritdoc/>
    public void GetFields(int pageIndex, List<FormField> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (IsDisposed || (uint)pageIndex >= (uint)PageCount)
        {
            return;
        }

        var page = HyperPdfLibrary.Document.PdfDocumentPages.GetPage(_document, pageIndex);
        List<PdfFormWidget> widgets = [];
        HyperPdfLibrary.Document.PdfDocumentForms.GetForm(_document).GetWidgets(pageIndex, widgets);
        foreach (var widget in widgets)
        {
            output.Add(ToField(widget, page.ToViewerRectangle(widget.Bounds)));
        }
    }

    /// <inheritdoc/>
    public void GetScripts(int pageIndex, List<FieldScripts> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (IsDisposed || (uint)pageIndex >= (uint)PageCount)
        {
            return;
        }

        List<PdfWidgetScripts> widgets = [];
        HyperPdfLibrary.Document.PdfDocumentForms.GetForm(_document).GetScripts(pageIndex, widgets);
        foreach (var widget in widgets)
        {
            FieldScripts scripts = new(
                widget.PageIndex,
                widget.Index,
                widget.Name,
                FormScript.Parse(widget.Keystroke),
                FormScript.Parse(widget.Format),
                FormScript.Parse(widget.Validate),
                FormScript.Parse(widget.Calculate));
            if (scripts.HasAny)
            {
                output.Add(scripts);
            }
        }
    }

    /// <inheritdoc/>
    public bool SetText(int pageIndex, int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (_editGate)
        {
            return !IsDisposed && FieldChanged(HyperPdfLibrary.Document.PdfDocumentForms.GetForm(_document).SetText(pageIndex, index, text));
        }
    }

    /// <inheritdoc/>
    public bool SetChecked(int pageIndex, int index, bool isChecked)
    {
        lock (_editGate)
        {
            return !IsDisposed && FieldChanged(HyperPdfLibrary.Document.PdfDocumentForms.GetForm(_document).SetChecked(pageIndex, index, isChecked));
        }
    }

    /// <inheritdoc/>
    public bool SelectOption(int pageIndex, int index, int option)
    {
        lock (_editGate)
        {
            return !IsDisposed && FieldChanged(HyperPdfLibrary.Document.PdfDocumentForms.GetForm(_document).SelectOption(pageIndex, index, option));
        }
    }

    /// <summary>Converts a library widget to the viewer's field.</summary>
    /// <param name="widget">The widget.</param>
    /// <param name="bounds">The widget's bounds in viewer space.</param>
    /// <returns>The field.</returns>
    private static FormField ToField(PdfFormWidget widget, PdfRectangle bounds) => new(
        widget.PageIndex,
        widget.Index,
        widget.Name,
        (FormFieldKind)(int)widget.Type,
        LinkTargets.ToPageRect(bounds),
        widget.Value,
        widget.IsChecked,
        widget.Options,
        widget.SelectedOption,
        widget.IsReadOnly,
        widget.IsRequired,
        widget.IsMultiline)
    { MaxLength = widget.MaxLength, IsComb = widget.IsComb, FontSize = widget.FontSize };
}
