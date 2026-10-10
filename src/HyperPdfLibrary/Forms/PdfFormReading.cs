// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reading operations over shared form state.</summary>
public static class PdfFormReading
{
    /// <summary>Determines whether the document has an interactive form.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <returns>True when an AcroForm dictionary is present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasForm(PdfForm form) => HyperPdfLibrary.Document.PdfDocumentContent.HasAcroForm(form.Document);

    /// <summary>Determines whether the form asks readers to rebuild field appearances.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <returns>The form's NeedAppearances flag.</returns>
    public static bool NeedAppearances(PdfForm form) => form.Document.Catalog.GetDictionary(KnownName.AcroForm)?.GetBoolean(KnownName.NeedAppearances) ?? false;

    /// <summary>Appends the widgets of a page, in <c>/Annots</c> order.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "output">The list receiving the widgets.</param>
    /// <exception cref = "ArgumentNullException"><paramref name = "output"/> is <see langword="null"/>.</exception>
    public static void GetWidgets(PdfForm form, int pageIndex, List<PdfFormWidget> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!PdfFormReading.HasForm(form) || PdfFormReading.GetAnnotations(form, pageIndex)is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (PdfFormReading.Resolve(form, pageIndex, annotations, i)is { } resolved)
            {
                output.Add(PdfFormReading.Describe(form, resolved));
            }
        }
    }

    /// <summary>
    /// Appends the scripts of the widgets of a page that have any, inherited from each field's parents. The library
    /// does not run them.
    /// </summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "output">The list receiving the scripts.</param>
    /// <exception cref = "ArgumentNullException"><paramref name = "output"/> is <see langword="null"/>.</exception>
    public static void GetScripts(PdfForm form, int pageIndex, List<PdfWidgetScripts> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!PdfFormReading.HasForm(form) || PdfFormReading.GetAnnotations(form, pageIndex)is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (PdfFormReading.Resolve(form, pageIndex, annotations, i)is { Type: not PdfFieldType.Unknown } resolved && PdfFormReading.ReadScripts(resolved)is { } scripts)
            {
                output.Add(scripts);
            }
        }
    }

    /// <summary>Reads a text field's character limit.</summary>
    /// <param name = "field">The field dictionary.</param>
    /// <returns>The limit, or 0 for none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ReadMaxLength(PdfDictionary field) => Math.Max(0, FieldAttributes.Find(field, KnownName.MaxLen).AsInt32());

    /// <summary>Reads a widget's additional actions.</summary>
    /// <param name = "resolved">The widget.</param>
    /// <returns>The scripts; <see langword="null"/> when it has none.</returns>
    internal static PdfWidgetScripts? ReadScripts(ResolvedWidget resolved)
    {
        var actions = FieldAttributes.Find(resolved.Field, KnownName.AA).AsDictionary() ?? resolved.Widget.GetDictionary(KnownName.AA);
        if (actions is null)
        {
            return null;
        }

        var keystroke = PdfFormReading.ReadScript(actions, KnownName.K);
        var format = PdfFormReading.ReadScript(actions, KnownName.F);
        var validate = PdfFormReading.ReadScript(actions, KnownName.V);
        var calculate = PdfFormReading.ReadScript(actions, KnownName.C);
        return keystroke.Length + format.Length + validate.Length + calculate.Length == 0 ? null : new(resolved.PageIndex, resolved.Index, resolved.Name, keystroke, format, validate, calculate);
    }

    /// <summary>Reads the JavaScript of one additional action.</summary>
    /// <param name = "actions">The <c>/AA</c> dictionary.</param>
    /// <param name = "key">The action's key.</param>
    /// <returns>The script; empty when the action has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ReadScript(PdfDictionary actions, KnownName key) => FieldAttributes.ReadText(actions.GetDictionary(key)?.Get(KnownName.JS) ?? default);

    /// <summary>Gets the <c>/Annots</c> array of a page.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <returns>The array; <see langword="null"/> when the page does not exist or has none.</returns>
    internal static PdfArray? GetAnnotations(PdfForm form, int pageIndex) => (uint)pageIndex < (uint)form.Document.PageCount
        ? HyperPdfLibrary.Document.PdfDocumentPages.GetPage(form.Document, pageIndex).Dictionary.GetArray(KnownName.Annots)
        : null;

    /// <summary>Finds a page's widget and the field it belongs to.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <returns>The widget; <see langword="null"/> when the annotation is missing or is not a widget.</returns>
    internal static ResolvedWidget? Resolve(PdfForm form, int pageIndex, int index) =>
        PdfFormReading.GetAnnotations(form, pageIndex) is { } annotations && (uint)index < (uint)annotations.Count
            ? PdfFormReading.Resolve(form, pageIndex, annotations, index)
            : null;

    /// <summary>Finds a widget and the field it belongs to.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "annotations">The page's annotations.</param>
    /// <param name = "index">The widget's index.</param>
    /// <returns>The widget; <see langword="null"/> when the annotation is missing or is not a widget.</returns>
    internal static ResolvedWidget? Resolve(PdfForm form, int pageIndex, PdfArray annotations, int index)
    {
        if (annotations.GetDictionary(index)is not { } widget || !widget.IsName(KnownName.Subtype, KnownName.Widget))
        {
            return null;
        }

        var widgetId = annotations.GetRaw(index).AsReference();
        var field = widget;
        var fieldId = widgetId;

        // A widget without a partial name of its own is a kid of the field that holds the field's attributes.
        if (!widget.ContainsKey(KnownName.T) && widget.GetDictionary(KnownName.Parent)is { } parent)
        {
            field = parent;
            fieldId = widget.GetRaw(KnownName.Parent).AsReference();
        }

        var name = FieldAttributes.GetFullName(widget);
        var source = FieldAttributes.Find(field, KnownName.FT).IsNull && widget.ContainsKey(KnownName.FT) ? widget : field;
        var flags = name.Length == 0 ? PdfFieldFlags.None : FieldAttributes.GetFlags(source);
        var type = name.Length == 0 ? PdfFieldType.Unknown : FormValues.GetType(source, form.Document.Objects.Names, flags);
        return new(pageIndex, index, widget, widgetId, field, fieldId, name, type, flags);
    }

    /// <summary>Describes a widget for callers.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "resolved">The widget.</param>
    /// <returns>The description.</returns>
    internal static PdfFormWidget Describe(PdfForm form, ResolvedWidget resolved)
    {
        var widget = resolved.Widget;
        var field = resolved.Field;
        var bounds = widget.TryGetRectangle(KnownName.Rect, out var rect) ? rect : default;
        var value = resolved.Type == PdfFieldType.Unknown ? string.Empty : PdfFormReading.ReadFieldValue(form, resolved);
        var isChecked = resolved.IsButton && new FormControl(widget, resolved.WidgetId).IsChecked(form.Document.Objects.Names);
        var options = resolved.IsChoice ? FormChoices.GetLabels(field) : [];
        var selected = resolved.IsChoice ? FormChoices.FirstSelected(field) : -1;
        var described = new PdfFormWidget(resolved.PageIndex, resolved.Index, resolved.Name, resolved.Type, bounds, value, isChecked, options, selected, resolved.Flags);
        return resolved.Type == PdfFieldType.Text
            ? described with
        {
            MaxLength = PdfFormReading.ReadMaxLength(field),
            FontSize = PdfFormReading.ReadFontSize(form, resolved)
        }
            : described;
    }

    /// <summary>Reads a field's value: the text, or for buttons the export value of the checked button.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "resolved">The widget.</param>
    /// <returns>The value.</returns>
    internal static string ReadFieldValue(PdfForm form, ResolvedWidget resolved)
    {
        if (!resolved.IsButton)
        {
            return FormValues.ReadText(resolved.Field, resolved.Type);
        }

        var controls = FormControl.GetControls(resolved.Field, resolved.FieldId);
        for (var i = 0; i < controls.Count; i++)
        {
            if (controls[i].IsChecked(form.Document.Objects.Names))
            {
                return PdfFormReading.GetExportValue(form, resolved.Field, controls, i);
            }
        }

        return FormValues.OffState;
    }

    /// <summary>Gets the value that stands for a button being on: its <c>/Opt</c> entry, or its on-state.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "field">The field dictionary.</param>
    /// <param name = "controls">The field's buttons.</param>
    /// <param name = "position">The button's position in the field.</param>
    /// <returns>The export value.</returns>
    internal static string GetExportValue(PdfForm form, PdfDictionary field, List<FormControl> controls, int position)
    {
        var export = string.Empty;
        if (FieldAttributes.Find(field, KnownName.Opt).AsArray()is { } options)
        {
            var entry = options.Get(position);
            export = entry.Kind == PdfKind.String ? PdfText.Decode(entry.AsStringBytes()) : FieldAttributes.ReadState(entry, form.Document.Objects.Names);
        }
        else
        {
            export = FieldAttributes.GetOnState(controls[position].Dictionary, form.Document.Objects.Names);
        }

        return export.Length == 0 ? FormValues.DefaultOnState : export;
    }

    /// <summary>Reads the size a text field's text is drawn at.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "resolved">The widget.</param>
    /// <returns>The size; 0 when the field fits the text to itself.</returns>
    internal static float ReadFontSize(PdfForm form, ResolvedWidget resolved)
    {
        var size = DefaultAppearance.Find(resolved.Widget, resolved.Field, form.Document.Catalog.GetDictionary(KnownName.AcroForm)).FontSize;
        return size > 0 ? size : 0;
    }
}
