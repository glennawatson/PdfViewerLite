// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using PdfViewerLite.Core.Forms;

namespace PdfViewerLite.Pdfium;

/// <summary>Form filling.</summary>
public sealed partial class PdfiumDocument
{
    /// <summary>The fields of each page read so far, dropped whenever any field changes (a field can recalculate others).</summary>
    private readonly Dictionary<int, FormField[]> _fieldCache = [];

    /// <inheritdoc/>
    public bool HasForm
    {
        get
        {
            using var scope = PdfiumLibrary.EnterScope();
            return !IsDisposed && _form.HasForm;
        }
    }

    /// <inheritdoc/>
    public void GetFields(int pageIndex, List<FormField> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        if (_fieldCache.TryGetValue(pageIndex, out var cached))
        {
            output.AddRange(cached);
            return;
        }

        if (IsDisposed || !_form.HasForm || GetPage(pageIndex) is not { } page)
        {
            return;
        }

        var start = output.Count;
        _form.Read(page, output);
        _fieldCache[pageIndex] = [.. CollectionsMarshal.AsSpan(output)[start..]];
    }

    /// <inheritdoc/>
    public bool SetText(int pageIndex, int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var scope = PdfiumLibrary.EnterScope();
        return FieldChanged(EditablePage(pageIndex) is { } page && _form.SetText(page, index, text));
    }

    /// <inheritdoc/>
    public bool SetChecked(int pageIndex, int index, bool isChecked)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return FieldChanged(EditablePage(pageIndex) is { } page && _form.SetChecked(page, index, isChecked));
    }

    /// <inheritdoc/>
    public bool SelectOption(int pageIndex, int index, int option)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return FieldChanged(EditablePage(pageIndex) is { } page && _form.SelectOption(page, index, option));
    }

    /// <summary>Records a field change and forgets every cached field.</summary>
    /// <param name="changed">Whether the change succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    private bool FieldChanged(bool changed)
    {
        if (changed)
        {
            _fieldCache.Clear();
            _ = Interlocked.Increment(ref _unsavedChanges);
        }

        return changed;
    }
}
