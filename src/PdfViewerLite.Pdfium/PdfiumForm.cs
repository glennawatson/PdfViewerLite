// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// A document's PDFium form fill environment. Fields are changed through PDFium's form logic (focus, select, type),
/// so appearances regenerate exactly as in other readers. Callers hold the PDFium lock. Filling allocates nothing:
/// text is passed pinned and strings are only built when fields are read.
/// </summary>
[DebuggerDisplay("Form: {HasForm}")]
internal sealed unsafe class PdfiumForm : IDisposable
{
    /// <summary>PDFium's widget annotation subtype.</summary>
    private const int SubtypeWidget = 20;

    /// <summary>The space character, which toggles a focused check box.</summary>
    private const int Space = ' ';

    /// <summary>The soft highlight that shows which areas can be filled in, as 0xRRGGBB.</summary>
    private const uint HighlightColor = 0xB4CCDCU;

    /// <summary>The opacity of the field highlight.</summary>
    private const byte HighlightAlpha = 72;

    /// <summary>The read-only field flag.</summary>
    private const int FlagReadOnly = 1;

    /// <summary>The required field flag.</summary>
    private const int FlagRequired = 1 << 1;

    /// <summary>The multi-line text field flag.</summary>
    private const int FlagMultiline = 1 << 12;

    /// <summary>The form environment; invalid when the document has no form.</summary>
    private readonly PdfiumFormHandle _handle;

    /// <summary>Initializes a new instance of the <see cref="PdfiumForm"/> class.</summary>
    /// <param name="document">The document.</param>
    internal PdfiumForm(PdfiumDocumentHandle document)
    {
        _handle = PdfiumFormHandle.Create(document);
        if (_handle.IsInvalid)
        {
            return;
        }

        NativeMethods.FPDF_SetFormFieldHighlightColor(_handle, 0, new(HighlightColor));
        NativeMethods.FPDF_SetFormFieldHighlightAlpha(_handle, HighlightAlpha);
    }

    /// <summary>Gets a value indicating whether the document has a fillable form.</summary>
    internal bool HasForm => !_handle.IsInvalid;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _handle.Dispose();

    /// <summary>Tells the form a page was loaded.</summary>
    /// <param name="page">The page.</param>
    internal void AfterLoad(PdfiumPageHandle page)
    {
        if (!HasForm)
        {
            return;
        }

        NativeMethods.FORM_OnAfterLoadPage(page, _handle);
    }

    /// <summary>Tells the form a page is about to close.</summary>
    /// <param name="page">The page.</param>
    internal void BeforeClose(PdfiumPageHandle page)
    {
        if (!HasForm)
        {
            return;
        }

        NativeMethods.FORM_OnBeforeClosePage(page, _handle);
    }

    /// <summary>Draws the form fields over a rendered page.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="page">The page.</param>
    /// <param name="placement">Where the page lies in the bitmap.</param>
    internal void Draw(nint bitmap, PdfiumPageHandle page, in PagePlacement placement)
    {
        if (!HasForm)
        {
            return;
        }

        NativeMethods.FPDF_FFLDraw(_handle, bitmap, page, placement.X, placement.Y, placement.Width, placement.Height, placement.Rotation, placement.Flags);
    }

    /// <summary>Appends the fields on a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="output">The list receiving the fields.</param>
    internal void Read(PdfiumPage page, List<FormField> output)
    {
        if (!HasForm)
        {
            return;
        }

        var count = NativeMethods.FPDFPage_GetAnnotCount(page.Handle);
        for (var i = 0; i < count; i++)
        {
            var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, i);
            if (annotation == 0)
            {
                continue;
            }

            try
            {
                if (NativeMethods.FPDFAnnot_GetSubtype(annotation) == SubtypeWidget && NativeMethods.FPDFAnnot_GetRect(annotation, out var rect) != 0)
                {
                    output.Add(ReadField(page, i, annotation, page.ToViewer(rect.Left, rect.Top, rect.Right, rect.Bottom)));
                }
            }
            finally
            {
                NativeMethods.FPDFPage_CloseAnnot(annotation);
            }
        }
    }

    /// <summary>Replaces a text field's text.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SetText(PdfiumPage page, int index, string text) => WithFocus(page, index, text, static (form, page, _, text) =>
    {
        _ = NativeMethods.FORM_SelectAllText(form, page.Handle);
        fixed (char* pointer = text)
        {
            NativeMethods.FORM_ReplaceSelection(form, page.Handle, pointer);
        }

        return true;
    });

    /// <summary>Turns a check box or radio button on or off.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="isChecked">The new state.</param>
    /// <returns><see langword="true"/> when the field ends in that state.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SetChecked(PdfiumPage page, int index, bool isChecked) => WithFocus(page, index, isChecked, static (form, page, annotation, isChecked) =>
    {
        if ((NativeMethods.FPDFAnnot_IsChecked(form, annotation) != 0) != isChecked)
        {
            _ = NativeMethods.FORM_OnChar(form, page.Handle, Space, 0);
        }

        return (NativeMethods.FPDFAnnot_IsChecked(form, annotation) != 0) == isChecked;
    });

    /// <summary>Selects a choice.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="option">The option index.</param>
    /// <returns><see langword="true"/> when selected.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SelectOption(PdfiumPage page, int index, int option) => WithFocus(page, index, option, static (form, page, _, option) =>
        NativeMethods.FORM_SetIndexSelected(form, page.Handle, option, 1) != 0);

    /// <summary>Maps PDFium's field type to a kind.</summary>
    /// <param name="type">The PDFium type.</param>
    /// <returns>The kind.</returns>
    private static FormFieldKind ToKind(int type) => type is >= (int)FormFieldKind.PushButton and <= (int)FormFieldKind.Signature ? (FormFieldKind)type : FormFieldKind.Unknown;

    /// <summary>Reads a UTF-16 string through a sizing call followed by a filling call.</summary>
    /// <typeparam name="TState">The reader's state.</typeparam>
    /// <param name="state">The state.</param>
    /// <param name="read">Calls PDFium with a buffer and its length in bytes, returning the needed length.</param>
    /// <returns>The string.</returns>
    private static string ReadUtf16<TState>(TState state, delegate*<TState, void*, CULong, CULong> read)
    {
        var length = (int)read(state, null, default).Value;
        if (length <= sizeof(char))
        {
            return string.Empty;
        }

        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            fixed (byte* buffer = rented)
            {
                _ = read(state, buffer, new((uint)length));
            }

            return NativeText.FromUtf16(rented.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Reads a field name.</summary>
    /// <param name="target">The form and widget.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The needed length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CULong ReadName((PdfiumFormHandle Form, nint Annotation) target, void* buffer, CULong length) =>
        NativeMethods.FPDFAnnot_GetFormFieldName(target.Form, target.Annotation, buffer, length);

    /// <summary>Reads a field value.</summary>
    /// <param name="target">The form and widget.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The needed length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CULong ReadValue((PdfiumFormHandle Form, nint Annotation) target, void* buffer, CULong length) =>
        NativeMethods.FPDFAnnot_GetFormFieldValue(target.Form, target.Annotation, buffer, length);

    /// <summary>Reads an option label.</summary>
    /// <param name="target">The form, widget and option index.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The needed length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CULong ReadOption((PdfiumFormHandle Form, nint Annotation, int Index) target, void* buffer, CULong length) =>
        NativeMethods.FPDFAnnot_GetOptionLabel(target.Form, target.Annotation, target.Index, buffer, length);

    /// <summary>Reads one field.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="bounds">The bounds in page space.</param>
    /// <returns>The field.</returns>
    private FormField ReadField(PdfiumPage page, int index, nint annotation, PageRect bounds)
    {
        var kind = ToKind(NativeMethods.FPDFAnnot_GetFormFieldType(_handle, annotation));
        var flags = NativeMethods.FPDFAnnot_GetFormFieldFlags(_handle, annotation);
        var target = (_handle, annotation);
        var name = ReadUtf16(target, &ReadName);
        var value = ReadUtf16(target, &ReadValue);
        var optionCount = kind is FormFieldKind.ComboBox or FormFieldKind.ListBox ? Math.Max(0, NativeMethods.FPDFAnnot_GetOptionCount(_handle, annotation)) : 0;
        var options = optionCount == 0 ? [] : new string[optionCount];
        var selected = -1;
        for (var i = 0; i < optionCount; i++)
        {
            options[i] = ReadUtf16((_handle, annotation, i), &ReadOption);
            if (selected < 0 && NativeMethods.FPDFAnnot_IsOptionSelected(_handle, annotation, i) != 0)
            {
                selected = i;
            }
        }

        var isChecked = kind is FormFieldKind.CheckBox or FormFieldKind.RadioButton && NativeMethods.FPDFAnnot_IsChecked(_handle, annotation) != 0;
        return new(page.Index, index, name, kind, bounds, value, isChecked, options, selected, (flags & FlagReadOnly) != 0, (flags & FlagRequired) != 0, (flags & FlagMultiline) != 0);
    }

    /// <summary>Focuses a widget, runs an edit on it, then removes the focus so PDFium commits the value.</summary>
    /// <typeparam name="TValue">The edit's value.</typeparam>
    /// <param name="page">The page.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="value">The value.</param>
    /// <param name="edit">The edit.</param>
    /// <returns>The edit's result, or <see langword="false"/> when the widget cannot be focused.</returns>
    private bool WithFocus<TValue>(PdfiumPage page, int index, TValue value, Func<PdfiumFormHandle, PdfiumPage, nint, TValue, bool> edit)
    {
        if (!HasForm)
        {
            return false;
        }

        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return false;
        }

        try
        {
            if (NativeMethods.FORM_SetFocusedAnnot(_handle, annotation) == 0)
            {
                return false;
            }

            var result = edit(_handle, page, annotation, value);
            _ = NativeMethods.FORM_ForceToKillFocus(_handle);
            return result;
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }
}
