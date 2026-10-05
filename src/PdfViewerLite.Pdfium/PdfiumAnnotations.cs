// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Reads and edits annotations through PDFium. Callers hold the PDFium lock. Adding and changing annotations makes no
/// managed allocations: strings are passed to PDFium pinned, and points are converted in stack or pooled buffers.
/// </summary>
internal static unsafe partial class PdfiumAnnotations
{
    /// <summary>PDFium's text note subtype.</summary>
    private const int SubtypeText = 1;

    /// <summary>PDFium's link subtype.</summary>
    private const int SubtypeLink = 2;

    /// <summary>PDFium's free text subtype.</summary>
    private const int SubtypeFreeText = 3;

    /// <summary>PDFium's highlight subtype.</summary>
    private const int SubtypeHighlight = 9;

    /// <summary>PDFium's underline subtype.</summary>
    private const int SubtypeUnderline = 10;

    /// <summary>PDFium's squiggly subtype.</summary>
    private const int SubtypeSquiggly = 11;

    /// <summary>PDFium's strike-out subtype.</summary>
    private const int SubtypeStrikeOut = 12;

    /// <summary>PDFium's stamp subtype.</summary>
    private const int SubtypeStamp = 13;

    /// <summary>PDFium's ink subtype.</summary>
    private const int SubtypeInk = 15;

    /// <summary>PDFium's pop-up subtype.</summary>
    private const int SubtypePopup = 16;

    /// <summary>PDFium's form widget subtype.</summary>
    private const int SubtypeWidget = 20;

    /// <summary>PDFium's XFA widget subtype.</summary>
    private const int SubtypeXfaWidget = 27;

    /// <summary>The annotation colour type (as opposed to the interior colour).</summary>
    private const int ColorTypeColor = 0;

    /// <summary>The normal appearance mode.</summary>
    private const int AppearanceNormal = 0;

    /// <summary>The "print" annotation flag, so annotations appear on paper too.</summary>
    private const int FlagPrint = 4;

    /// <summary>Fully opaque.</summary>
    private const uint Opaque = 255;

    /// <summary>The maximum channel value.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The size of a sticky note icon in points.</summary>
    private const float NoteSize = 20;

    /// <summary>The line height as a multiple of the font size.</summary>
    private const float LineHeight = 1.25F;

    /// <summary>How far below the top of a line its baseline sits, as a multiple of the font size.</summary>
    private const float Ascent = 0.8F;

    /// <summary>Points converted on the stack before a pooled buffer is used.</summary>
    private const int StackPoints = 128;

    /// <summary>PDFium's round line cap and round line join, so ink strokes keep soft ends and corners.</summary>
    private const int RoundLine = 1;

    /// <summary>Characters copied on the stack before a pooled buffer is used.</summary>
    private const int StackChars = 256;

    /// <summary>The size of the buffer strings are read into before falling back to the heap.</summary>
    private const int ReadBufferBytes = 1024;

    /// <summary>The PDF date pattern for the modification date.</summary>
    private const string DateFormat = "'D:'yyyyMMddHHmmss'Z'";

    /// <summary>The longest formatted PDF date plus a terminator.</summary>
    private const int DateChars = 24;

    /// <summary>The <c>/Subj</c> marking a drawn or typed signature.</summary>
    private const string SignatureSubject = "Signature";

    /// <summary>The <c>/Subj</c> marking text written on the page.</summary>
    private const string TextBoxSubject = "Text box";

    /// <summary>The user name recorded as the author, read once.</summary>
    private static readonly string Author = Environment.UserName;

    /// <summary>Gets or sets the clock used for modification dates; tests replace it.</summary>
    internal static TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>Gets the dictionary key of the note text.</summary>
    private static ReadOnlySpan<byte> ContentsKey => "Contents"u8;

    /// <summary>Appends the annotations on a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="output">The list receiving the annotations.</param>
    internal static void Read(PdfiumPage page, List<PageAnnotation> output)
    {
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
                var kind = GetKind(annotation);
                if (kind is not { } known || IsReply(annotation) || NativeMethods.FPDFAnnot_GetRect(annotation, out var rect) == 0)
                {
                    continue;
                }

                var bounds = page.ToViewer(rect.Left, rect.Top, rect.Right, rect.Bottom);
                var color = GetColor(annotation, known);
                output.Add(new(page.Index, i, known, bounds, color, ReadString(annotation, ContentsKey), ReadString(annotation, "T"u8)));
            }
            finally
            {
                NativeMethods.FPDFPage_CloseAnnot(annotation);
            }
        }
    }

    /// <summary>Adds a text markup annotation.</summary>
    /// <param name="page">The page.</param>
    /// <param name="kind">The markup kind.</param>
    /// <param name="lines">The marked line rectangles.</param>
    /// <param name="color">The colour.</param>
    /// <param name="contents">The note text.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddMarkup(PdfiumPage page, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        var subtype = kind switch
        {
            AnnotationKind.Highlight => SubtypeHighlight,
            AnnotationKind.Underline => SubtypeUnderline,
            AnnotationKind.StrikeOut => SubtypeStrikeOut,
            AnnotationKind.Squiggly => SubtypeSquiggly,
            _ => 0,
        };
        if (subtype == 0 || lines.IsEmpty)
        {
            return -1;
        }

        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, subtype);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            PdfBounds bounds = default;
            foreach (var line in lines)
            {
                var quad = ToQuad(page, line, ref bounds);
                _ = NativeMethods.FPDFAnnot_AppendAttachmentPoints(annotation, quad);
            }

            Finish(annotation, bounds.ToRect(0), color, contents, null);
            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Adds an ink annotation.</summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The points, stroke after stroke.</param>
    /// <param name="strokeLengths">The number of points in each stroke.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <param name="kind">Ink or signature.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddInk(PdfiumPage page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind)
    {
        if (points.IsEmpty || strokeLengths.IsEmpty)
        {
            return -1;
        }

        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeInk);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            PdfBounds bounds = default;
            nint path = 0;
            var offset = 0;
            foreach (var length in strokeLengths)
            {
                if (length <= 0 || offset + length > points.Length)
                {
                    break;
                }

                AddStroke(page, annotation, points.Slice(offset, length), ref bounds, ref path);
                offset += length;
            }

            _ = NativeMethods.FPDFAnnot_SetBorder(annotation, 0, 0, width);
            Finish(annotation, bounds.ToRect(width), color, string.Empty, kind == AnnotationKind.Signature ? SignatureSubject : null);
            AppendInkAppearance(annotation, path, color, width);
            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Adds a sticky note.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The top-left corner of the icon.</param>
    /// <param name="contents">The note text.</param>
    /// <param name="color">The colour.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddNote(PdfiumPage page, PagePoint location, string contents, uint color)
    {
        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeText);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            PdfBounds bounds = default;
            bounds.Add(page, location);
            bounds.Add(page, new(location.X + NoteSize, location.Y + NoteSize));
            Finish(annotation, bounds.ToRect(0), color, contents, null);
            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Adds text on the page as a stamp holding one text object per line.</summary>
    /// <param name="fonts">The document's fonts, loaded once.</param>
    /// <param name="page">The page.</param>
    /// <param name="location">The top-left corner of the first line.</param>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="color">The colour.</param>
    /// <param name="kind">Text box or signature.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddText(PdfiumFonts fonts, PdfiumPage page, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return -1;
        }

        var signature = kind == AnnotationKind.Signature;
        var font = fonts.Get(signature);
        return font is null ? -1 : AddTextObjects(fonts.Document, page, new(font, fontSize, color), location, text, signature ? SignatureSubject : TextBoxSubject);
    }

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetColor(PdfiumPage page, int index, uint color)
    {
        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return false;
        }

        try
        {
            var subtype = NativeMethods.FPDFAnnot_GetSubtype(annotation);
            if (subtype == SubtypeStamp)
            {
                return RecolorObjects(annotation, color);
            }

            // Ink made here keeps its strokes as an appearance path, so recolour the path rather than drop it, or the
            // ink would no longer print.
            if (subtype == SubtypeInk && NativeMethods.FPDFAnnot_GetObjectCount(annotation) > 0)
            {
                var recoloured = RecolorObjects(annotation, color);
                return SetAnnotationColor(annotation, color) || recoloured;
            }

            // PDFium only regenerates the appearance of supported subtypes once the old one is cleared.
            _ = NativeMethods.FPDFAnnot_SetAP(annotation, AppearanceNormal, null);
            return SetAnnotationColor(annotation, color);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Changes an annotation's note text.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="contents">The note text.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetContents(PdfiumPage page, int index, string contents)
    {
        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return false;
        }

        try
        {
            return SetString(annotation, ContentsKey, contents) && SetModified(annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Maps a PDFium subtype to a kind, or <see langword="null"/> for annotations the viewer does not list.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The kind.</returns>
    private static AnnotationKind? GetKind(nint annotation) => NativeMethods.FPDFAnnot_GetSubtype(annotation) switch
    {
        SubtypeLink or SubtypePopup or SubtypeWidget or SubtypeXfaWidget => null,
        SubtypeText => AnnotationKind.Note,
        SubtypeHighlight => AnnotationKind.Highlight,
        SubtypeUnderline => AnnotationKind.Underline,
        SubtypeSquiggly => AnnotationKind.Squiggly,
        SubtypeStrikeOut => AnnotationKind.StrikeOut,
        SubtypeFreeText => AnnotationKind.TextBox,
        SubtypeInk => GetInkKind(annotation),
        SubtypeSquare => AnnotationKind.Rectangle,
        SubtypeCircle => AnnotationKind.Ellipse,
        SubtypeStamp => GetStampKind(annotation),
        _ => AnnotationKind.Other,
    };

    /// <summary>Tells signatures and text boxes this viewer wrote apart from other stamps.</summary>
    /// <param name="annotation">The stamp.</param>
    /// <returns>The kind.</returns>
    private static AnnotationKind GetStampKind(nint annotation)
    {
        if (HasSubject(annotation, SignatureSubject))
        {
            return AnnotationKind.Signature;
        }

        if (HasSubject(annotation, StampSubject))
        {
            return AnnotationKind.Stamp;
        }

        return HasSubject(annotation, TextBoxSubject) ? AnnotationKind.TextBox : AnnotationKind.Other;
    }

    /// <summary>Tells signatures, arrows and lines this viewer drew apart from freehand ink.</summary>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>The kind.</returns>
    private static AnnotationKind GetInkKind(nint annotation)
    {
        if (HasSubject(annotation, SignatureSubject))
        {
            return AnnotationKind.Signature;
        }

        if (HasSubject(annotation, ArrowSubject))
        {
            return AnnotationKind.Arrow;
        }

        return HasSubject(annotation, LineSubject) ? AnnotationKind.Line : AnnotationKind.Ink;
    }

    /// <summary>Reads an annotation's colour, falling back to the ink colour for stamps drawn from page objects.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns>The colour as 0xRRGGBB.</returns>
    private static uint GetColor(nint annotation, AnnotationKind kind)
    {
        if (NativeMethods.FPDFAnnot_GetColor(annotation, ColorTypeColor, out var red, out var green, out var blue, out _) != 0)
        {
            return ((red & ChannelMask) << RedShift) | ((green & ChannelMask) << GreenShift) | (blue & ChannelMask);
        }

        return kind is AnnotationKind.Signature or AnnotationKind.TextBox ? AnnotationColors.Ink : AnnotationColors.Sand;
    }

    /// <summary>Determines whether an annotation's <c>/Subj</c> equals a value, without allocating.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subject">The subject.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    private static bool HasSubject(nint annotation, string subject)
    {
        Span<byte> buffer = stackalloc byte[ReadBufferBytes];
        int length;
        fixed (byte* key = "Subj"u8)
        {
            fixed (byte* pointer = buffer)
            {
                length = (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, key, pointer, new((uint)buffer.Length)).Value;
            }
        }

        if (length <= sizeof(char) || length > buffer.Length)
        {
            return false;
        }

        return MemoryMarshal.Cast<byte, char>(buffer[..(length - sizeof(char))]).SequenceEqual(subject);
    }

    /// <summary>Reads a string value from the annotation dictionary.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The null terminated key.</param>
    /// <returns>The value, or an empty string.</returns>
    private static string ReadString(nint annotation, ReadOnlySpan<byte> key)
    {
        fixed (byte* keyPointer = key)
        {
            var length = (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, keyPointer, null, default).Value;
            if (length <= sizeof(char))
            {
                return string.Empty;
            }

            var rented = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                fixed (byte* buffer = rented)
                {
                    _ = NativeMethods.FPDFAnnot_GetStringValue(annotation, keyPointer, buffer, new((uint)length));
                }

                return NativeText.FromUtf16(rented.AsSpan(0, length));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Converts a line rectangle to quad points in PDF space and grows the bounds.</summary>
    /// <param name="page">The page.</param>
    /// <param name="line">The line in page space.</param>
    /// <param name="bounds">The bounds so far.</param>
    /// <returns>The quad points.</returns>
    private static FsQuadPointsF ToQuad(PdfiumPage page, PageRect line, ref PdfBounds bounds)
    {
        page.ToPdf(new(line.Left, line.Top), out var x1, out var y1);
        page.ToPdf(new(line.Right, line.Top), out var x2, out var y2);
        page.ToPdf(new(line.Left, line.Bottom), out var x3, out var y3);
        page.ToPdf(new(line.Right, line.Bottom), out var x4, out var y4);
        bounds.Add(x1, y1);
        bounds.Add(x2, y2);
        bounds.Add(x3, y3);
        bounds.Add(x4, y4);
        return new((float)x1, (float)y1, (float)x2, (float)y2, (float)x3, (float)y3, (float)x4, (float)y4);
    }

    /// <summary>
    /// Converts one stroke to PDF space, adds it to an ink annotation, and draws it into the path that becomes the
    /// annotation's appearance.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The ink annotation.</param>
    /// <param name="stroke">The stroke in page space.</param>
    /// <param name="bounds">The bounds so far.</param>
    /// <param name="path">The appearance path, created by the first stroke.</param>
    private static void AddStroke(PdfiumPage page, nint annotation, ReadOnlySpan<PagePoint> stroke, ref PdfBounds bounds, ref nint path)
    {
        FsPointF[]? rented = null;
        var converted = stroke.Length <= StackPoints ? stackalloc FsPointF[StackPoints] : (rented = ArrayPool<FsPointF>.Shared.Rent(stroke.Length));
        try
        {
            for (var i = 0; i < stroke.Length; i++)
            {
                page.ToPdf(stroke[i], out var x, out var y);
                bounds.Add(x, y);
                converted[i] = new((float)x, (float)y);
            }

            fixed (FsPointF* points = converted)
            {
                _ = NativeMethods.FPDFAnnot_AddInkStroke(annotation, points, (nuint)stroke.Length);
            }

            DrawStroke(converted[..stroke.Length], ref path);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<FsPointF>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Adds one stroke to the appearance path as joined lines; a single point draws a dot with the round cap.</summary>
    /// <param name="points">The stroke in PDF space.</param>
    /// <param name="path">The appearance path, created by the first stroke.</param>
    private static void DrawStroke(ReadOnlySpan<FsPointF> points, ref nint path)
    {
        if (points.IsEmpty)
        {
            return;
        }

        if (path == 0)
        {
            path = NativeMethods.FPDFPageObj_CreateNewPath(points[0].X, points[0].Y);
            if (path == 0)
            {
                return;
            }
        }
        else
        {
            _ = NativeMethods.FPDFPath_MoveTo(path, points[0].X, points[0].Y);
        }

        if (points.Length == 1)
        {
            _ = NativeMethods.FPDFPath_LineTo(path, points[0].X, points[0].Y);
            return;
        }

        for (var i = 1; i < points.Length; i++)
        {
            _ = NativeMethods.FPDFPath_LineTo(path, points[i].X, points[i].Y);
        }
    }

    /// <summary>
    /// Gives an ink annotation an appearance stream from its strokes, so it prints and flattens like it shows: PDFium
    /// draws ink without one on screen, but flattening for print and other readers need the stream.
    /// </summary>
    /// <param name="annotation">The ink annotation, with its rectangle set.</param>
    /// <param name="path">The strokes as a path, or 0 when there were none; ownership passes to the annotation.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    private static void AppendInkAppearance(nint annotation, nint path, uint color, float width)
    {
        if (path == 0)
        {
            return;
        }

        _ = NativeMethods.FPDFPath_SetDrawMode(path, 0, 1);
        _ = NativeMethods.FPDFPageObj_SetStrokeWidth(path, width);
        _ = NativeMethods.FPDFPageObj_SetLineCap(path, RoundLine);
        _ = NativeMethods.FPDFPageObj_SetLineJoin(path, RoundLine);
        _ = NativeMethods.FPDFPageObj_SetStrokeColor(path, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
        if (NativeMethods.FPDFAnnot_AppendObject(annotation, path) == 0)
        {
            NativeMethods.FPDFPageObj_Destroy(path);
        }
    }

    /// <summary>Creates the text objects of a stamp, sizes the stamp to them and appends them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="style">The font, size and colour.</param>
    /// <param name="location">The top-left corner.</param>
    /// <param name="text">The text.</param>
    /// <param name="subject">The <c>/Subj</c> recording what the stamp is.</param>
    /// <returns>The annotation index, or -1.</returns>
    private static int AddTextObjects(PdfiumDocumentHandle document, PdfiumPage page, in TextStyle style, PagePoint location, string text, string subject)
    {
        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeStamp);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            page.ToPdf(location, out var left, out var top);
            Span<nint> objects = stackalloc nint[StackPoints];
            var count = 0;
            PdfBounds bounds = default;
            foreach (var line in text.AsSpan().EnumerateLines())
            {
                if (count == objects.Length)
                {
                    break;
                }

                var baseline = top - (style.FontSize * Ascent) - (count * style.FontSize * LineHeight);
                objects[count] = CreateTextObject(document, style, line, left, baseline, ref bounds);
                count++;
            }

            // The stamp's appearance box comes from its rectangle, so set it before appending the objects.
            Finish(annotation, bounds.ToRect(0), style.Color, string.Empty, subject);
            foreach (var textObject in objects[..count])
            {
                _ = NativeMethods.FPDFAnnot_AppendObject(annotation, textObject);
            }

            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Creates one positioned line of text.</summary>
    /// <param name="document">The document.</param>
    /// <param name="style">The font, size and colour.</param>
    /// <param name="line">The line.</param>
    /// <param name="x">The PDF x of the line start.</param>
    /// <param name="baseline">The PDF y of the baseline.</param>
    /// <param name="bounds">The bounds so far.</param>
    /// <returns>The text object.</returns>
    private static nint CreateTextObject(PdfiumDocumentHandle document, in TextStyle style, ReadOnlySpan<char> line, double x, double baseline, ref PdfBounds bounds)
    {
        var color = style.Color;
        var textObject = NativeMethods.FPDFPageObj_CreateTextObj(document, style.Font, style.FontSize);
        char[]? rented = null;
        var terminated = line.Length < StackChars ? stackalloc char[StackChars] : (rented = ArrayPool<char>.Shared.Rent(line.Length + 1));
        try
        {
            line.CopyTo(terminated);
            terminated[line.Length] = '\0';
            fixed (char* pointer = terminated)
            {
                _ = NativeMethods.FPDFText_SetText(textObject, pointer);
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        _ = NativeMethods.FPDFPageObj_SetFillColor(textObject, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
        NativeMethods.FPDFPageObj_Transform(textObject, 1, 0, 0, 1, x, baseline);
        if (NativeMethods.FPDFPageObj_GetBounds(textObject, out var left, out var bottom, out var right, out var top) != 0)
        {
            bounds.Add(left, bottom);
            bounds.Add(right, top);
        }

        return textObject;
    }

    /// <summary>Recolours the page objects of a stamp.</summary>
    /// <param name="annotation">The stamp.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when at least one object changed.</returns>
    private static bool RecolorObjects(nint annotation, uint color)
    {
        var changed = false;
        var count = NativeMethods.FPDFAnnot_GetObjectCount(annotation);
        for (var i = 0; i < count; i++)
        {
            var pageObject = NativeMethods.FPDFAnnot_GetObject(annotation, i);
            if (pageObject == 0)
            {
                continue;
            }

            _ = NativeMethods.FPDFPageObj_SetFillColor(pageObject, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
            _ = NativeMethods.FPDFPageObj_SetStrokeColor(pageObject, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
            changed |= NativeMethods.FPDFAnnot_UpdateObject(annotation, pageObject) != 0;
        }

        return changed && SetModified(annotation);
    }

    /// <summary>Sets the rectangle, colour, contents, author, date, subject and print flag of a new annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rect">The rectangle in PDF space.</param>
    /// <param name="color">The colour.</param>
    /// <param name="contents">The note text.</param>
    /// <param name="subject">The <c>/Subj</c>, or <see langword="null"/>.</param>
    private static void Finish(nint annotation, in FsRectF rect, uint color, string contents, string? subject)
    {
        _ = NativeMethods.FPDFAnnot_SetRect(annotation, rect);
        _ = SetAnnotationColor(annotation, color);
        _ = NativeMethods.FPDFAnnot_SetFlags(annotation, FlagPrint);
        _ = SetString(annotation, "T"u8, Author);
        _ = SetModified(annotation);
        if (contents.Length > 0)
        {
            _ = SetString(annotation, ContentsKey, contents);
        }

        if (subject is not null)
        {
            _ = SetString(annotation, "Subj"u8, subject);
        }
    }

    /// <summary>Sets an annotation's colour.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when set.</returns>
    private static bool SetAnnotationColor(nint annotation, uint color) =>
        NativeMethods.FPDFAnnot_SetColor(annotation, ColorTypeColor, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque) != 0;

    /// <summary>Sets a string value; .NET strings are null terminated in memory, so they are passed pinned without copying.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The null terminated key.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when set.</returns>
    private static bool SetString(nint annotation, ReadOnlySpan<byte> key, string value)
    {
        fixed (byte* keyPointer = key)
        {
            fixed (char* valuePointer = value)
            {
                return NativeMethods.FPDFAnnot_SetStringValue(annotation, keyPointer, valuePointer) != 0;
            }
        }
    }

    /// <summary>Records the current time as the modification date, formatted on the stack.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when set.</returns>
    private static bool SetModified(nint annotation)
    {
        Span<char> date = stackalloc char[DateChars];
        if (!Clock.GetUtcNow().UtcDateTime.TryFormat(date, out var written, DateFormat, CultureInfo.InvariantCulture) || written >= date.Length)
        {
            return false;
        }

        date[written] = '\0';
        fixed (byte* key = "M"u8)
        {
            fixed (char* value = date)
            {
                return NativeMethods.FPDFAnnot_SetStringValue(annotation, key, value) != 0;
            }
        }
    }
}
