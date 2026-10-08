// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;
using PdfViewerLite.Pdfium.Native;
using PdfViewerLite.Pdfium.Text;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Formatted text boxes. The text is laid out once, written as positioned glyphs in an embedded subset font (or a built
/// in one) inside a stamp's appearance, and becomes a standard typewriter free text annotation, with its default
/// appearance, default style and rich text entries, when the file is saved. The text and every
/// setting are kept too, so the box can be edited again after the file is reopened.
/// </summary>
internal static unsafe partial class PdfiumAnnotations
{
    /// <summary>The slant of drawn-in italic: the tangent of 12 degrees.</summary>
    private const float ItalicSlant = 0.2126F;

    /// <summary>The outline width of drawn-in bold, as a share of the font size.</summary>
    private const float BoldStroke = 0.03F;

    /// <summary>PDFium's fill then stroke text render mode, used to draw bold in.</summary>
    private const int RenderFillStroke = 2;

    /// <summary>The glyphs of a run converted on the stack before a pooled buffer is used.</summary>
    private const int StackGlyphs = 256;

    /// <summary>The narrowest box, in points, so an empty line still has a place.</summary>
    private const float MinBoxWidth = 1;

    /// <summary>The characters the save-time entries take besides the text.</summary>
    /// <summary>The start of the free text entries, up to the default appearance.</summary>
    private const string EntriesStart = "/Subtype /FreeText /IT /FreeTextTypeWriter /DA (";

    /// <summary>The end of the default appearance and the key of the default style.</summary>
    private const string StyleKey = ") /DS ";

    /// <summary>The key of the rich text.</summary>
    private const string RichTextKey = " /RC ";

    /// <summary>The thinnest underline, in points.</summary>
    private const float MinUnderline = 0.5F;

    /// <summary>How far apart, in points, two glyphs' baselines may be and still share a run.</summary>
    private const float BaselineTolerance = 0.001F;

    /// <summary>Gets the key holding a text box's format record.</summary>
    private static ReadOnlySpan<byte> FormatKey => "PVLFormat"u8;

    /// <summary>Writes a formatted text box.</summary>
    /// <param name="fonts">The document's fonts.</param>
    /// <param name="page">The page.</param>
    /// <param name="location">The box's top-left corner.</param>
    /// <param name="content">The width lines wrap at (or 0), and the text.</param>
    /// <param name="format">The format.</param>
    /// <param name="author">The author recorded.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddTextBox(PdfiumFonts fonts, PdfiumPage page, PagePoint location, (float WrapWidth, string Text) content, TextFormat format, string author)
    {
        var text = content.Text;
        if (string.IsNullOrWhiteSpace(text) || fonts.Resolve(format, text) is not { } font)
        {
            return -1;
        }

        var layout = fonts.Layout;
        layout.Layout(text, format, font.Shaper, content.WrapWidth);
        FontSubset? subset = null;
        var handle = font.Program is { } program ? fonts.Embed(program, text, layout.Glyphs, out subset) : font.Standard?.Font;
        if (handle is null)
        {
            return -1;
        }

        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeStamp);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            page.ToPdf(location, out var left, out var top);
            var rect = new FsRectF((float)left, (float)top, (float)left + Math.Max(layout.Width, MinBoxWidth), (float)top - layout.Height);

            // The stamp's appearance box comes from its rectangle, so set it before appending the objects.
            Finish(annotation, rect, format.Color, text, TextBoxSubject, author);
            WriteTextBoxKeys(annotation, text, format, content.WrapWidth);
            var pen = new TextPen(fonts.Document, handle, subset, font, format, (float)left, (float)top);
            AppendLines(annotation, layout, text, pen);
            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Reads a text box's text, format and place: one written here, an older one, or free text from another program.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The content, or <see langword="null"/> when the annotation is not editable text.</returns>
    internal static TextBoxContent? GetTextBox(PdfiumPage page, int index)
    {
        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return null;
        }

        try
        {
            if (GetKind(annotation) != AnnotationKind.TextBox || IsRemoved(annotation) || NativeMethods.FPDFAnnot_GetRect(annotation, out var rect) == 0)
            {
                return null;
            }

            var bounds = page.ToViewer(rect.Left, rect.Top, rect.Right, rect.Bottom);
            var text = ReadString(annotation, TextKey);
            if (TryReadFormat(annotation, out var format, out var wrap))
            {
                return new(text, format, bounds, wrap);
            }

            return NativeMethods.FPDFAnnot_GetSubtype(annotation) == SubtypeStamp
                ? OlderTextBox(annotation, text, bounds)
                : ForeignTextBox(annotation, bounds);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Reads a text box written by an older version of this viewer: Helvetica at a recorded size.</summary>
    /// <param name="annotation">The stamp.</param>
    /// <param name="text">Its text.</param>
    /// <param name="bounds">Its bounds.</param>
    /// <returns>The content, or <see langword="null"/> without text.</returns>
    private static TextBoxContent? OlderTextBox(nint annotation, string text, PageRect bounds)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var size = ReadNumber(annotation, FontSizeKey);
        var format = TextFormat.Default with { FontSize = size > 0 ? size : TextFormat.Default.FontSize, Color = GetColor(annotation, AnnotationKind.TextBox) };
        return new(text, format, bounds, 0);
    }

    /// <summary>Reads free text written by another program from its contents and its default appearance and style.</summary>
    /// <param name="annotation">The free text annotation.</param>
    /// <param name="bounds">Its bounds.</param>
    /// <returns>The content.</returns>
    private static TextBoxContent ForeignTextBox(nint annotation, PageRect bounds)
    {
        var text = ReadString(annotation, ContentsKey);
        var format = FreeTextStyle.ApplyDefaultAppearance(ReadString(annotation, "DA"u8), TextFormat.Default);
        format = FreeTextStyle.ApplyDefaultStyle(ReadString(annotation, "DS"u8), format).Clamped();
        return new(text, format, bounds, bounds.Width);
    }

    /// <summary>Writes the standard free text entries kept until the file is saved, as one string of its exact length.</summary>
    /// <param name="appearance">The default appearance.</param>
    /// <param name="style">The default style.</param>
    /// <param name="richText">The rich text.</param>
    /// <returns>The entries.</returns>
    private static string FreeTextEntries(string appearance, string style, string richText)
    {
        var length = EntriesStart.Length + appearance.Length + StyleKey.Length + TextFormatCodec.HexTextStringLength(style.Length)
            + RichTextKey.Length + TextFormatCodec.HexTextStringLength(richText.Length);
        return string.Create(length, (appearance, style, richText), static (destination, parts) =>
        {
            EntriesStart.CopyTo(destination);
            var at = EntriesStart.Length;
            parts.appearance.CopyTo(destination[at..]);
            at += parts.appearance.Length;
            StyleKey.CopyTo(destination[at..]);
            at += StyleKey.Length;
            at += TextFormatCodec.WriteHexTextString(parts.style, destination[at..]);
            RichTextKey.CopyTo(destination[at..]);
            at += RichTextKey.Length;
            _ = TextFormatCodec.WriteHexTextString(parts.richText, destination[at..]);
        });
    }

    /// <summary>Gets whether an annotation is a text box written here, which keeps a format record.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when it has a record.</returns>
    private static bool HasFormat(nint annotation)
    {
        fixed (byte* key = FormatKey)
        {
            return (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, key, null, default).Value > sizeof(char);
        }
    }

    /// <summary>Reads the format record kept with a text box written here, decoding it in a pooled buffer.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="format">The format read.</param>
    /// <param name="wrapWidth">The wrap width read.</param>
    /// <returns><see langword="true"/> when the box has a record.</returns>
    private static bool TryReadFormat(nint annotation, out TextFormat format, out float wrapWidth)
    {
        fixed (byte* key = FormatKey)
        {
            var length = (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, key, null, default).Value;
            if (length <= sizeof(char))
            {
                format = TextFormat.Default;
                wrapWidth = 0;
                return false;
            }

            var rented = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                fixed (byte* buffer = rented)
                {
                    _ = NativeMethods.FPDFAnnot_GetStringValue(annotation, key, buffer, new((uint)length));
                }

                var chars = MemoryMarshal.Cast<byte, char>(rented.AsSpan(0, length & ~1));
                var terminator = chars.IndexOf('\0');
                return TextFormatCodec.TryRead(terminator >= 0 ? chars[..terminator] : chars, out format, out wrapWidth);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Records the text, format and the standard free text entries written when the file is saved.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <param name="wrapWidth">The wrap width.</param>
    private static void WriteTextBoxKeys(nint annotation, string text, TextFormat format, float wrapWidth)
    {
        _ = SetString(annotation, TextKey, text);
        _ = SetNumber(annotation, FontSizeKey, format.FontSize);
        _ = SetString(annotation, FormatKey, TextFormatCodec.Write(format, wrapWidth));
        _ = SetString(annotation, EntriesKey, FreeTextEntries(FreeTextStyle.DefaultAppearance(format), FreeTextStyle.DefaultStyle(format), FreeTextStyle.RichText(text, format)));
    }

    /// <summary>Writes each line's glyph runs and underline into the annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="text">The text.</param>
    /// <param name="pen">How glyphs are written.</param>
    private static void AppendLines(nint annotation, TextBoxLayout layout, string text, in TextPen pen)
    {
        var glyphs = layout.Glyphs;
        var lines = layout.Lines;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var end = line.GlyphStart + line.GlyphCount;
            var runStart = line.GlyphStart;
            for (var i = line.GlyphStart + 1; i <= end; i++)
            {
                // A glyph raised or lowered from the baseline, such as a placed mark, starts a run of its own.
                if (i < end && Math.Abs(glyphs[i].Y - glyphs[runStart].Y) < BaselineTolerance)
                {
                    continue;
                }

                AppendRun(annotation, glyphs[runStart..i], text, pen);
                runStart = i;
            }

            if (pen.Format.IsUnderline && line.Width > 0)
            {
                AppendUnderline(annotation, layout, line, pen);
            }
        }
    }

    /// <summary>Writes one run of glyphs on a shared baseline as a text object with each glyph's position.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="run">The glyphs.</param>
    /// <param name="text">The text, for built in fonts which are written by character.</param>
    /// <param name="pen">How glyphs are written.</param>
    private static void AppendRun(nint annotation, ReadOnlySpan<LaidGlyph> run, string text, in TextPen pen)
    {
        if (run.IsEmpty)
        {
            return;
        }

        var textObject = NativeMethods.FPDFPageObj_CreateTextObj(pen.Document, pen.Font, pen.Format.FontSize);
        if (textObject == 0)
        {
            return;
        }

        if (!SetGlyphs(textObject, run, text, pen))
        {
            NativeMethods.FPDFPageObj_Destroy(textObject);
            return;
        }

        StyleText(textObject, pen);
        var slant = pen.TextFont.FakeItalic ? ItalicSlant : 0;
        NativeMethods.FPDFPageObj_Transform(textObject, 1, 0, slant, 1, pen.Left + run[0].X, pen.Top - run[0].Y);
        if (NativeMethods.FPDFAnnot_AppendObject(annotation, textObject) == 0)
        {
            NativeMethods.FPDFPageObj_Destroy(textObject);
        }
    }

    /// <summary>Sets a text object's glyphs, by code for an embedded font or by character for a built in one, and their positions.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="run">The glyphs.</param>
    /// <param name="text">The text.</param>
    /// <param name="pen">How glyphs are written.</param>
    /// <returns><see langword="true"/> when set.</returns>
    private static bool SetGlyphs(nint textObject, ReadOnlySpan<LaidGlyph> run, string text, in TextPen pen)
    {
        var count = run.Length;
        float[]? rentedPositions = null;
        var positions = count <= StackGlyphs ? stackalloc float[StackGlyphs] : (rentedPositions = ArrayPool<float>.Shared.Rent(count));
        try
        {
            if (!(pen.Subset is { } subset ? SetCodes(textObject, run, subset) : SetCharacters(textObject, run, text)))
            {
                return false;
            }

            for (var i = 1; i < count; i++)
            {
                positions[i - 1] = run[i].X - run[0].X;
            }

            fixed (float* pointer = positions)
            {
                return count == 1 || NativeMethods.FPDFText_SetPositions(textObject, pointer, (nuint)(count - 1)) != 0;
            }
        }
        finally
        {
            if (rentedPositions is not null)
            {
                ArrayPool<float>.Shared.Return(rentedPositions);
            }
        }
    }

    /// <summary>Sets an embedded font's glyph codes: each glyph's number in the subset.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="run">The glyphs.</param>
    /// <param name="subset">The embedded subset.</param>
    /// <returns><see langword="true"/> when set.</returns>
    private static bool SetCodes(nint textObject, ReadOnlySpan<LaidGlyph> run, FontSubset subset)
    {
        uint[]? rented = null;
        var codes = run.Length <= StackGlyphs ? stackalloc uint[StackGlyphs] : (rented = ArrayPool<uint>.Shared.Rent(run.Length));
        try
        {
            for (var i = 0; i < run.Length; i++)
            {
                codes[i] = subset.Map(run[i].Glyph);
            }

            fixed (uint* pointer = codes)
            {
                return NativeMethods.FPDFText_SetCharcodes(textObject, pointer, (nuint)run.Length) != 0;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<uint>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Sets a built in font's characters; its glyphs are the characters themselves.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="run">The glyphs.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when set.</returns>
    private static bool SetCharacters(nint textObject, ReadOnlySpan<LaidGlyph> run, string text)
    {
        char[]? rented = null;
        var characters = run.Length < StackGlyphs ? stackalloc char[StackGlyphs] : (rented = ArrayPool<char>.Shared.Rent(run.Length + 1));
        try
        {
            for (var i = 0; i < run.Length; i++)
            {
                characters[i] = (uint)run[i].Cluster < (uint)text.Length ? text[run[i].Cluster] : (char)run[i].Glyph;
            }

            characters[run.Length] = '\0';
            fixed (char* pointer = characters)
            {
                return NativeMethods.FPDFText_SetText(textObject, pointer) != 0;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Colours a text object, and outlines it as well when bold is drawn in.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="pen">How glyphs are written.</param>
    private static void StyleText(nint textObject, in TextPen pen)
    {
        var color = pen.Format.Color;
        _ = NativeMethods.FPDFPageObj_SetFillColor(textObject, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
        if (!pen.TextFont.FakeBold)
        {
            return;
        }

        _ = NativeMethods.FPDFTextObj_SetTextRenderMode(textObject, RenderFillStroke);
        _ = NativeMethods.FPDFPageObj_SetStrokeColor(textObject, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
        _ = NativeMethods.FPDFPageObj_SetStrokeWidth(textObject, pen.Format.FontSize * BoldStroke);
    }

    /// <summary>Draws a line's underline as a filled bar under its ink.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="line">The line.</param>
    /// <param name="pen">How glyphs are written.</param>
    private static void AppendUnderline(nint annotation, TextBoxLayout layout, in LaidLine line, in TextPen pen)
    {
        var thickness = Math.Max(layout.UnderlineThickness, MinUnderline);
        var y = pen.Top - (line.Baseline + layout.UnderlineOffset) - (thickness * Half);
        var bar = NativeMethods.FPDFPageObj_CreateNewRect(pen.Left + line.Left, y, line.Width, thickness);
        if (bar == 0)
        {
            return;
        }

        var color = pen.Format.Color;
        _ = NativeMethods.FPDFPath_SetDrawMode(bar, 1, 0);
        _ = NativeMethods.FPDFPageObj_SetFillColor(bar, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
        if (NativeMethods.FPDFAnnot_AppendObject(annotation, bar) == 0)
        {
            NativeMethods.FPDFPageObj_Destroy(bar);
        }
    }

    /// <summary>What writing a text box's glyphs needs.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Font">The loaded font.</param>
    /// <param name="Subset">The embedded subset, or <see langword="null"/> for a built in font.</param>
    /// <param name="TextFont">The font choice, with the styles drawn in.</param>
    /// <param name="Format">The format.</param>
    /// <param name="Left">The PDF x of the box's left edge.</param>
    /// <param name="Top">The PDF y of the box's top edge.</param>
    private readonly record struct TextPen(PdfiumDocumentHandle Document, PdfiumFontHandle Font, FontSubset? Subset, TextFont TextFont, TextFormat Format, float Left, float Top);
}
