// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Text written on the page in a built in font: text boxes and typed signatures as stamps holding one line of text
/// per line, and framed stamps such as "APPROVED". Lines are measured from the font's glyph boxes, as PDF readers
/// measure text, so the annotation fits its ink.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>The line height as a multiple of the font size.</summary>
    private const float LineHeight = 1.25F;

    /// <summary>How far below the top of a line its baseline sits, as a multiple of the font size.</summary>
    private const float Ascent = 0.8F;

    /// <summary>The most lines written; later lines are dropped, as the PDFium engine drops them.</summary>
    private const int MaxLines = 128;

    /// <summary>The font size of a stamp's word.</summary>
    private const float StampFontSize = 18;

    /// <summary>The space between a stamp's word and its frame.</summary>
    private const float StampPadding = 4;

    /// <summary>The width of a stamp's frame.</summary>
    private const float StampFrame = 2;

    /// <summary>Gets the name of the one font an appearance written here uses.</summary>
    private static ReadOnlySpan<byte> FontResource => "F1"u8;

    /// <inheritdoc/>
    public int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return -1;
        }

        var signature = kind == AnnotationKind.Signature;
        var font = signature ? AppearanceFont.TimesItalic : AppearanceFont.Helvetica;
        lock (_gate)
        {
            if (GetPage(pageIndex) is not { } page)
            {
                return -1;
            }

            var origin = ToUser(page, location);
            var bounds = MeasureLines(font, fontSize, text, origin);
            if (!bounds.IsSet)
            {
                return -1;
            }

            var rectangle = bounds.ToRectangle(0);
            var stamp = PdfAnnotations.Create(_store, KnownName.Stamp, rectangle);
            if (!signature)
            {
                PdfAnnotations.SetText(stamp, _names.Text, text);
                PdfAnnotations.SetNumberText(stamp, _names.FontSize, fontSize);
            }

            SetTextAppearance(stamp, rectangle, new(font, fontSize), color, text, origin);
            return Add(pageIndex, page, stamp, color, string.Empty, signature ? SignatureSubject : TextBoxSubject);
        }
    }

    /// <inheritdoc/>
    public int AddStamp(int pageIndex, PagePoint location, string label, uint color)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (string.IsNullOrWhiteSpace(label))
        {
            return -1;
        }

        lock (_gate)
        {
            if (GetPage(pageIndex) is not { } page)
            {
                return -1;
            }

            var origin = ToUser(page, new(location.X + StampPadding + StampFrame, location.Y + StampPadding + StampFrame));
            var ink = MeasureLines(AppearanceFont.Helvetica, StampFontSize, label, origin);
            if (!ink.IsSet)
            {
                return -1;
            }

            var rectangle = ink.ToRectangle(StampPadding + StampFrame);
            var stamp = PdfAnnotations.Create(_store, KnownName.Stamp, rectangle);
            var builder = default(PdfContentBuilder);
            try
            {
                builder.SaveState();
                PdfAppearances.SetColors(ref builder, color);
                var box = ink.ToRectangle(StampPadding);
                builder.SetLineWidth(StampFrame);
                builder.Rectangle(box.Left, box.Bottom, box.Width, box.Height);
                builder.Stroke();
                WriteLines(ref builder, new(AppearanceFont.Helvetica, StampFontSize), label, origin);
                builder.RestoreState();
                _ = PdfAnnotations.SetNormalAppearance(_store, stamp, builder.ToFormXObject(_store, rectangle, CreateFontResources(AppearanceFont.Helvetica)));
            }
            finally
            {
                builder.Dispose();
            }

            return Add(pageIndex, page, stamp, color, label, StampSubject);
        }
    }

    /// <inheritdoc/>
    public bool SetFontSize(int pageIndex, int index, float fontSize)
    {
        if (fontSize <= 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (!TryEdit(pageIndex, index, out var page, out var annotation) || !CanLayOutAgain(annotation, out var kind))
            {
                return false;
            }

            var text = PdfAnnotations.GetText(annotation, _names.Text);
            var color = GetColor(annotation, kind);
            if (kind == AnnotationKind.Callout)
            {
                RebuildCallout(annotation, text, fontSize, color);
            }
            else
            {
                RelayText(annotation, text, fontSize, color);
            }

            SetModified(annotation);
            return Commit(pageIndex, page, index, annotation);
        }
    }

    /// <summary>Measures the ink of lines of text, the first line's top at a corner.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The size.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    /// <returns>The bounds, unset when no line has a character the font can show.</returns>
    private static UserBounds MeasureLines(AppearanceFont font, float size, ReadOnlySpan<char> text, Vector2 origin)
    {
        var bounds = default(UserBounds);
        var builder = default(PdfContentBuilder);
        try
        {
            VisitLines(ref builder, new(font, size), text, origin, ref bounds, false);
        }
        finally
        {
            builder.Dispose();
        }

        return bounds;
    }

    /// <summary>Writes lines of text, the first line's top at a corner.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    private static void WriteLines(ref PdfContentBuilder builder, TextLook style, ReadOnlySpan<char> text, Vector2 origin)
    {
        var bounds = default(UserBounds);
        VisitLines(ref builder, style, text, origin, ref bounds, true);
    }

    /// <summary>Measures, and optionally writes, each line of text.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    /// <param name="bounds">The ink bounds so far.</param>
    /// <param name="write">Whether to write the lines.</param>
    private static void VisitLines(ref PdfContentBuilder builder, TextLook style, ReadOnlySpan<char> text, Vector2 origin, ref UserBounds bounds, bool write)
    {
        var count = 0;
        foreach (var line in text.EnumerateLines())
        {
            if (count == MaxLines)
            {
                return;
            }

            var baseline = origin.Y - (style.Size * Ascent) - (count * style.Size * LineHeight);
            VisitLine(ref builder, style, line, new(origin.X, baseline), ref bounds, write);
            count++;
        }
    }

    /// <summary>Measures, and optionally writes, one line of text in WinAnsi codes.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="line">The line.</param>
    /// <param name="start">Where the baseline starts.</param>
    /// <param name="bounds">The ink bounds so far.</param>
    /// <param name="write">Whether to write the line.</param>
    private static void VisitLine(ref PdfContentBuilder builder, TextLook style, ReadOnlySpan<char> line, Vector2 start, ref UserBounds bounds, bool write)
    {
        // The codes go to the content builder, which may keep a span it is given, so they live in a pooled array.
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(line.Length, 1));
        var codes = rented.AsSpan();
        try
        {
            var count = AppearanceFontMetrics.Encode(line, codes);
            if (count == 0)
            {
                return;
            }

            var ink = AppearanceFontMetrics.MeasureInk(style.Font, codes[..count], style.Size);
            bounds.AddRectangle(new(start.X + ink.Left, start.Y + ink.Bottom, start.X + ink.Right, start.Y + ink.Top));
            if (!write)
            {
                return;
            }

            builder.BeginText();
            builder.SetFont(FontResource, style.Size);
            builder.SetTextMatrix(1, 0, 0, 1, start.X, start.Y);
            builder.ShowText(codes[..count]);
            builder.EndText();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Creates resources holding one built in font as <c>/F1</c>.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The resources.</returns>
    private PdfDictionary CreateFontResources(AppearanceFont font)
    {
        var fonts = new PdfDictionary(_store, 1);
        fonts.Set(_store.Names.Intern(FontResource), PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(_store, font)));
        var resources = new PdfDictionary(_store, 1);
        resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        return resources;
    }

    /// <summary>Determines whether an annotation is a text box or callout written here with a built in font, which can be laid out again.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    private bool CanLayOutAgain(PdfDictionary annotation, out AnnotationKind kind)
    {
        kind = GetKind(annotation) ?? AnnotationKind.Other;
        var shape = annotation.IsName(KnownName.Subtype, KnownName.Stamp) || (kind == AnnotationKind.Callout && annotation.IsName(KnownName.Subtype, KnownName.FreeText));

        // Formatted text boxes are laid out again by writing them anew, so their fonts and settings are kept.
        return shape && kind is AnnotationKind.TextBox or AnnotationKind.Callout && !IsRemoved(annotation)
            && IsWrittenHere(annotation) && !PdfAnnotations.HasText(annotation, _names.Format);
    }

    /// <summary>Lays a text box out again at a new size, keeping its top-left corner where its ink starts.</summary>
    /// <param name="annotation">The text box.</param>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The new size.</param>
    /// <param name="color">The colour.</param>
    private void RelayText(PdfDictionary annotation, string text, float fontSize, uint color)
    {
        var old = PdfAnnotations.GetRectangle(annotation);
        var corner = new Vector2(old.Left, old.Top);
        var ink = MeasureLines(AppearanceFont.Helvetica, fontSize, text, corner).ToRectangle(0);
        var origin = corner + new Vector2(corner.X - ink.Left, corner.Y - ink.Top);
        var rectangle = new PdfRectangle(old.Left, old.Top - ink.Height, old.Left + ink.Width, old.Top);
        PdfAnnotations.SetRectangle(annotation, rectangle);
        PdfAnnotations.SetNumberText(annotation, _names.FontSize, fontSize);
        SetTextAppearance(annotation, rectangle, new(AppearanceFont.Helvetica, fontSize), color, text, origin);
    }

    /// <summary>Gives an annotation an appearance of lines of text.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rectangle">Its rectangle, the appearance's box.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="color">The colour.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    private void SetTextAppearance(PdfDictionary annotation, PdfRectangle rectangle, TextLook style, uint color, string text, Vector2 origin)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            PdfAppearances.SetColors(ref builder, color);
            WriteLines(ref builder, style, text, origin);
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(_store, annotation, builder.ToFormXObject(_store, rectangle, CreateFontResources(style.Font)));
        }
        finally
        {
            builder.Dispose();
        }
    }
}
