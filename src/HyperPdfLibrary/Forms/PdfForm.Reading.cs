// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <content>Reading widgets, their values and their scripts.</content>
public sealed partial class PdfForm
{
    /// <summary>Appends the widgets of a page, in <c>/Annots</c> order.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the widgets.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void GetWidgets(int pageIndex, List<PdfFormWidget> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!HasForm || GetAnnotations(pageIndex) is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (Resolve(pageIndex, annotations, i) is { } resolved)
            {
                output.Add(Describe(resolved));
            }
        }
    }

    /// <summary>
    /// Appends the scripts of the widgets of a page that have any, inherited from each field's parents. The library
    /// does not run them.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the scripts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void GetScripts(int pageIndex, List<PdfWidgetScripts> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!HasForm || GetAnnotations(pageIndex) is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (Resolve(pageIndex, annotations, i) is { Type: not PdfFieldType.Unknown } resolved && ReadScripts(resolved) is { } scripts)
            {
                output.Add(scripts);
            }
        }
    }

    /// <summary>Reads a text field's character limit.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The limit, or 0 for none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadMaxLength(PdfDictionary field) => Math.Max(0, FieldAttributes.Find(field, KnownName.MaxLen).AsInt32());

    /// <summary>Reads a widget's additional actions.</summary>
    /// <param name="resolved">The widget.</param>
    /// <returns>The scripts; <see langword="null"/> when it has none.</returns>
    private static PdfWidgetScripts? ReadScripts(ResolvedWidget resolved)
    {
        var actions = FieldAttributes.Find(resolved.Field, KnownName.AA).AsDictionary() ?? resolved.Widget.GetDictionary(KnownName.AA);
        if (actions is null)
        {
            return null;
        }

        var keystroke = ReadScript(actions, KnownName.K);
        var format = ReadScript(actions, KnownName.F);
        var validate = ReadScript(actions, KnownName.V);
        var calculate = ReadScript(actions, KnownName.C);
        return keystroke.Length + format.Length + validate.Length + calculate.Length == 0
            ? null
            : new(resolved.PageIndex, resolved.Index, resolved.Name, keystroke, format, validate, calculate);
    }

    /// <summary>Reads the JavaScript of one additional action.</summary>
    /// <param name="actions">The <c>/AA</c> dictionary.</param>
    /// <param name="key">The action's key.</param>
    /// <returns>The script; empty when the action has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ReadScript(PdfDictionary actions, KnownName key) =>
        FieldAttributes.ReadText(actions.GetDictionary(key)?.Get(KnownName.JS) ?? default);

    /// <summary>Gets the <c>/Annots</c> array of a page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The array; <see langword="null"/> when the page does not exist or has none.</returns>
    private PdfArray? GetAnnotations(int pageIndex) =>
        (uint)pageIndex < (uint)_document.PageCount ? _document.GetPage(pageIndex).Dictionary.GetArray(KnownName.Annots) : null;

    /// <summary>Finds a page's widget and the field it belongs to.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget's index in the page's annotations.</param>
    /// <returns>The widget; <see langword="null"/> when the annotation is missing or is not a widget.</returns>
    private ResolvedWidget? Resolve(int pageIndex, int index) =>
        GetAnnotations(pageIndex) is { } annotations && (uint)index < (uint)annotations.Count ? Resolve(pageIndex, annotations, index) : null;

    /// <summary>Finds a widget and the field it belongs to.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="annotations">The page's annotations.</param>
    /// <param name="index">The widget's index.</param>
    /// <returns>The widget; <see langword="null"/> when the annotation is missing or is not a widget.</returns>
    private ResolvedWidget? Resolve(int pageIndex, PdfArray annotations, int index)
    {
        if (annotations.GetDictionary(index) is not { } widget || !widget.IsName(KnownName.Subtype, KnownName.Widget))
        {
            return null;
        }

        var widgetId = annotations.GetRaw(index).AsReference();
        var field = widget;
        var fieldId = widgetId;

        // A widget without a partial name of its own is a kid of the field that holds the field's attributes.
        if (!widget.ContainsKey(KnownName.T) && widget.GetDictionary(KnownName.Parent) is { } parent)
        {
            field = parent;
            fieldId = widget.GetRaw(KnownName.Parent).AsReference();
        }

        var name = FieldAttributes.GetFullName(widget);
        var source = FieldAttributes.Find(field, KnownName.FT).IsNull && widget.ContainsKey(KnownName.FT) ? widget : field;
        var flags = name.Length == 0 ? PdfFieldFlags.None : FieldAttributes.GetFlags(source);
        var type = name.Length == 0 ? PdfFieldType.Unknown : FormValues.GetType(source, Store.Names, flags);
        return new(pageIndex, index, widget, widgetId, field, fieldId, name, type, flags);
    }

    /// <summary>Describes a widget for callers.</summary>
    /// <param name="resolved">The widget.</param>
    /// <returns>The description.</returns>
    private PdfFormWidget Describe(ResolvedWidget resolved)
    {
        var widget = resolved.Widget;
        var field = resolved.Field;
        var bounds = widget.TryGetRectangle(KnownName.Rect, out var rect) ? rect : default;
        var value = resolved.Type == PdfFieldType.Unknown ? string.Empty : ReadFieldValue(resolved);
        var isChecked = resolved.IsButton && new FormControl(widget, resolved.WidgetId).IsChecked(Store.Names);
        var options = resolved.IsChoice ? FormChoices.GetLabels(field) : [];
        var selected = resolved.IsChoice ? FormChoices.FirstSelected(field) : -1;
        var described = new PdfFormWidget(resolved.PageIndex, resolved.Index, resolved.Name, resolved.Type, bounds, value, isChecked, options, selected, resolved.Flags);
        return resolved.Type == PdfFieldType.Text ? described with { MaxLength = ReadMaxLength(field), FontSize = ReadFontSize(resolved) } : described;
    }

    /// <summary>Reads a field's value: the text, or for buttons the export value of the checked button.</summary>
    /// <param name="resolved">The widget.</param>
    /// <returns>The value.</returns>
    private string ReadFieldValue(ResolvedWidget resolved)
    {
        if (!resolved.IsButton)
        {
            return FormValues.ReadText(resolved.Field, resolved.Type);
        }

        var controls = FormControl.GetControls(resolved.Field, resolved.FieldId);
        for (var i = 0; i < controls.Count; i++)
        {
            if (controls[i].IsChecked(Store.Names))
            {
                return GetExportValue(resolved.Field, controls, i);
            }
        }

        return FormValues.OffState;
    }

    /// <summary>Gets the value that stands for a button being on: its <c>/Opt</c> entry, or its on-state.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="controls">The field's buttons.</param>
    /// <param name="position">The button's position in the field.</param>
    /// <returns>The export value.</returns>
    private string GetExportValue(PdfDictionary field, List<FormControl> controls, int position)
    {
        var export = string.Empty;
        if (FieldAttributes.Find(field, KnownName.Opt).AsArray() is { } options)
        {
            var entry = options.Get(position);
            export = entry.Kind == PdfKind.String ? PdfText.Decode(entry.AsStringBytes()) : FieldAttributes.ReadState(entry, Store.Names);
        }
        else
        {
            export = FieldAttributes.GetOnState(controls[position].Dictionary, Store.Names);
        }

        return export.Length == 0 ? FormValues.DefaultOnState : export;
    }

    /// <summary>Reads the size a text field's text is drawn at.</summary>
    /// <param name="resolved">The widget.</param>
    /// <returns>The size; 0 when the field fits the text to itself.</returns>
    private float ReadFontSize(ResolvedWidget resolved)
    {
        var size = DefaultAppearance.Find(resolved.Widget, resolved.Field, AcroForm).FontSize;
        return size > 0 ? size : 0;
    }
}
