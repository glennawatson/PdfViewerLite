// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Draws text and widget annotation appearances.</summary>
internal static class AnnotationTextAppearance
{
    /// <summary>The side of the note icon PDFium draws for a Text annotation, in points.</summary>
    private const float NoteSize = 20;

    /// <summary>How far the note icon's speech tail reaches.</summary>
    private const float TipDelta = 4;

    /// <summary>The inset of the note icon's lines from its sides.</summary>
    private const float LineInset = 2;

    /// <summary>The lines inside the note icon.</summary>
    private const int NoteLines = 3;

    /// <summary>The parts the note icon's height is split into for its lines.</summary>
    private const float NoteLineParts = 4;

    /// <summary>Draws a Text annotation as PDFium does: a 20 point note icon at the bottom-left of /Rect.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance.</returns>
    internal static GeneratedAppearance? DrawTextIcon(AnnotationContext context)
    {
        var rect = AnnotationAppearanceGeometry.Rect(context.Annotation);
        var note = new PdfRectangle(rect.Left, rect.Bottom, rect.Left + NoteSize, rect.Bottom + NoteSize);
        var builder = default(PdfContentBuilder);
        try
        {
            AnnotationAppearanceContent.Begin(ref builder);
            builder.SetFillRgb(1, 1, 0);
            builder.SetStrokeRgb(0, 0, 0);
            builder.SetLineWidth(1);
            AddNoteOutline(ref builder, note);
            AddNoteLines(ref builder, note);
            builder.FillEvenOddAndStroke();
            return AnnotationAppearanceResources.Finish(ref builder, context, note, AnnotationAppearanceResources.CreateResources(context, KnownName.Normal));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// Draws a FreeText annotation as PDFium does: the /C background, a border in the text colour, and the /Contents
    /// laid out with the /DA font, size and colour by the form text layout.
    /// </summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance.</returns>
    internal static GeneratedAppearance? DrawFreeText(AnnotationContext context)
    {
        var annotation = context.Annotation;
        var rect = AnnotationAppearanceGeometry.Rect(annotation);
        var form = context.Store.Catalog.GetDictionary(KnownName.AcroForm);
        var appearance = DefaultAppearance.Find(annotation, annotation, form);
        var width = annotation.GetDictionary(KnownName.BS) is { } style && style.ContainsKey(KnownName.W) ? style.GetSingle(KnownName.W) : 1;
        var builder = default(PdfContentBuilder);
        try
        {
            AnnotationAppearanceContent.Begin(ref builder);
            var background = AnnotationAppearanceGeometry.Deflate(rect, width * AnnotationAppearanceGeometry.Half);
            if (annotation.GetArray(KnownName.C) is { } color && FormAppearanceColor.TryWriteColor(ref builder, color, false))
            {
                builder.Rectangle(background.Left, background.Bottom, background.Width, background.Height);
                builder.Fill();
            }

            if (width > 0)
            {
                builder.WriteRaw(Encoding.ASCII.GetBytes($"{appearance.StrokeColor}\n"));
                builder.SetLineWidth(width);
                AnnotationAppearanceBorder.WriteDash(ref builder, annotation);
                builder.Rectangle(background.Left, background.Bottom, background.Width, background.Height);
                builder.Stroke();
            }

            var widget = new AppearanceContext(context.Store, annotation, annotation, PdfFieldType.Text, PdfFieldFlags.Multiline, form, context.Page.Resources) { Fonts = context.Cache.Fonts };
            var text = FormTextAppearance.CreateText(widget, annotation.GetText(KnownName.Contents) ?? string.Empty);
            var frame = AnnotationAppearanceResources.CreateForm(builder.ToArray(), context, rect, AnnotationAppearanceResources.CreateResources(context, KnownName.Normal));
            return new(frame, text, rect);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// Draws a text or choice widget from its value when the form sets /NeedAppearances, as PDFium regenerates them.
    /// Buttons, signatures and widgets of forms that do not ask are left alone.
    /// </summary>
    /// <param name="context">The widget.</param>
    /// <returns>The appearance, or null.</returns>
    internal static GeneratedAppearance? DrawWidget(AnnotationContext context)
    {
        var form = context.Store.Catalog.GetDictionary(KnownName.AcroForm);
        if (form is null || !form.GetBoolean(KnownName.NeedAppearances))
        {
            return null;
        }

        var widget = context.Annotation;
        var field = !widget.ContainsKey(KnownName.T) && widget.GetDictionary(KnownName.Parent) is { } parent ? parent : widget;
        var source = FieldAttributes.Find(field, KnownName.FT).IsNull && widget.ContainsKey(KnownName.FT) ? widget : field;
        var flags = FieldAttributes.GetFlags(source);
        var type = FormValues.GetType(source, context.Store.Names, flags);
        if (type is not (PdfFieldType.Text or PdfFieldType.ComboBox or PdfFieldType.ListBox))
        {
            return null;
        }

        var appearance = new AppearanceContext(context.Store, widget, field, type, flags, form, context.Page.Resources) { Fonts = context.Cache.Fonts };
        return new(CreateWidgetForm(appearance), null, AnnotationAppearanceGeometry.Rect(widget));
    }

    /// <summary>Draws a text, combo box or list box widget from its value.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The Form XObject.</returns>
    private static PdfStream CreateWidgetForm(AppearanceContext context)
    {
        if (context.Type == PdfFieldType.ListBox)
        {
            return FormTextAppearance.CreateList(context);
        }

        var text = FormValues.ReadText(context.Field, context.Type);
        if (context.Type == PdfFieldType.ComboBox)
        {
            var option = FormChoices.FindValue(context.Field, text);
            text = option >= 0 ? FormChoices.GetLabel(context.Field, option) : text;
        }

        return FormTextAppearance.CreateText(context, text);
    }

    /// <summary>Adds the note icon's outline: a box with a speech tail at the bottom-left.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="note">The icon's rectangle.</param>
    private static void AddNoteOutline(ref PdfContentBuilder builder, PdfRectangle note)
    {
        var outer = AnnotationAppearanceGeometry.Deflate(note, AnnotationAppearanceGeometry.Half);
        outer = outer with { Bottom = outer.Bottom + TipDelta };
        var tailLeft = outer.Left + TipDelta;
        var tailRight = tailLeft + TipDelta;
        var tailTop = outer.Bottom - TipDelta;
        builder.MoveTo(outer.Left, outer.Bottom);
        builder.LineTo(outer.Left, outer.Top);
        builder.LineTo(outer.Right, outer.Top);
        builder.LineTo(outer.Right, outer.Bottom);
        builder.LineTo(tailRight, outer.Bottom);
        builder.LineTo((tailLeft + tailRight) * AnnotationAppearanceGeometry.Half, tailTop);
        builder.LineTo(tailLeft, outer.Bottom);
        builder.LineTo(outer.Left, outer.Bottom);
    }

    /// <summary>Adds the three lines inside the note icon.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="note">The icon's rectangle.</param>
    private static void AddNoteLines(ref PdfContentBuilder builder, PdfRectangle note)
    {
        var outer = AnnotationAppearanceGeometry.Deflate(note, AnnotationAppearanceGeometry.Half);
        outer = outer with { Bottom = outer.Bottom + TipDelta };
        var step = (outer.Top - outer.Bottom) / NoteLineParts;
        var top = outer.Top;
        for (var i = 0; i < NoteLines; i++)
        {
            top -= step;
            builder.MoveTo(outer.Left + LineInset, top);
            builder.LineTo(outer.Right - LineInset, top);
        }
    }
}
