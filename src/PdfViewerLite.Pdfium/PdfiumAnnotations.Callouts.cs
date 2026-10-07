// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Callouts, and laying text out again at a new size. PDFium cannot draw free text, so a callout is made as a stamp
/// holding its text, frame and leader line as page objects, and becomes a standard callout (<c>/Subtype /FreeText</c>,
/// <c>/IT /FreeTextCallout</c> and the leader line's <c>/CL</c> points) when the file is saved; its drawn appearance
/// is kept, so every reader shows it the same way.
/// </summary>
internal static partial class PdfiumAnnotations
{
    /// <summary>The subject marking a callout.</summary>
    private const string CalloutSubject = "Callout";

    /// <summary>The width of a callout's frame and leader line.</summary>
    private const float CalloutLine = 1;

    /// <summary>The space between a callout's text and its frame.</summary>
    private const float CalloutPadding = 4;

    /// <summary>The page objects of a callout besides its text: the frame and the leader line.</summary>
    private const int CalloutShapes = 2;

    /// <summary>How far a callout's text sits inside its frame's edge.</summary>
    private const float CalloutInset = CalloutPadding + CalloutLine;

    /// <summary>Gets the key holding the top-left corner of a callout's text, in PDF space, so it can be laid out again.</summary>
    private static ReadOnlySpan<byte> AnchorKey => "PVLAnchor"u8;

    /// <summary>Writes a callout: text in a frame with a line and arrow pointing at the target.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="points">The point the arrow points at, and the top-left corner of the text.</param>
    /// <param name="style">The font, size and colour.</param>
    /// <param name="text">The text.</param>
    /// <param name="author">The author recorded.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddCallout(PdfiumDocumentHandle document, PdfiumPage page, (PagePoint Target, PagePoint Location) points, in TextStyle style, string text, string author)
    {
        if (string.IsNullOrWhiteSpace(text) || style.FontSize <= 0)
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
            page.ToPdf(points.Target, out var targetX, out var targetY);
            page.ToPdf(points.Location, out var left, out var top);
            Span<nint> objects = stackalloc nint[StackPoints + CalloutShapes];
            var layout = new CalloutLayout((float)targetX, (float)targetY, (float)left, (float)top);
            var count = BuildCallout(document, style, text, ref layout, objects);

            // The stamp's appearance box comes from its rectangle, so set it before appending the objects.
            Finish(annotation, layout.Rect, style.Color, text, CalloutSubject, author);
            WriteCalloutKeys(annotation, text, style.FontSize, style.Color, layout);
            foreach (var pageObject in objects[..count])
            {
                _ = NativeMethods.FPDFAnnot_AppendObject(annotation, pageObject);
            }

            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Lays a text box or callout written here out again at a new text size, keeping its top-left corner.</summary>
    /// <param name="fonts">The document's fonts.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="fontSize">The new size.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetFontSize(PdfiumFonts fonts, PdfiumPage page, int index, float fontSize)
    {
        if (fontSize <= 0 || fonts.Get(false) is not { } font || !ReadTextLayout(page, index, out var text, out var kind, out var color, out var layout))
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
            Span<nint> objects = stackalloc nint[StackPoints + CalloutShapes];
            TextStyle style = new(font, fontSize, color);
            int count;
            if (kind == AnnotationKind.Callout)
            {
                count = BuildCallout(fonts.Document, style, text, ref layout, objects);
                WriteCalloutKeys(annotation, text, fontSize, color, layout);
            }
            else
            {
                PdfBounds bounds = default;
                count = CreateTextLines(fonts.Document, style, text, layout.Left, layout.Top, objects, ref bounds);
                layout.Rect = Align(objects[..count], bounds.ToRect(0), layout.Left, layout.Top);
                _ = SetNumber(annotation, FontSizeKey, fontSize);
            }

            _ = NativeMethods.FPDFAnnot_SetRect(annotation, layout.Rect);
            foreach (var pageObject in objects[..count])
            {
                _ = NativeMethods.FPDFAnnot_AppendObject(annotation, pageObject);
            }

            return SetModified(annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>
    /// Reads what laying a text box or callout out again needs, then drops its appearance so the new objects start a
    /// fresh one fitted to the new rectangle.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="text">The text.</param>
    /// <param name="kind">Text box or callout.</param>
    /// <param name="color">The colour.</param>
    /// <param name="layout">The callout's target and the text's top-left corner.</param>
    /// <returns><see langword="true"/> when the annotation can be laid out again.</returns>
    private static bool ReadTextLayout(PdfiumPage page, int index, out string text, out AnnotationKind kind, out uint color, out CalloutLayout layout)
    {
        text = string.Empty;
        kind = AnnotationKind.Other;
        color = 0;
        layout = default;
        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return false;
        }

        try
        {
            if (NativeMethods.FPDFAnnot_GetSubtype(annotation) != SubtypeStamp || IsRemoved(annotation) || NativeMethods.FPDFAnnot_GetRect(annotation, out var rect) == 0)
            {
                return false;
            }

            kind = GetStampKind(annotation);
            text = ReadString(annotation, TextKey);
            if (text.Length == 0 || kind is not (AnnotationKind.TextBox or AnnotationKind.Callout))
            {
                return false;
            }

            color = GetColor(annotation, kind);
            layout = new(rect.Left, rect.Bottom, rect.Left, rect.Top);
            if (kind == AnnotationKind.Callout)
            {
                ReadCalloutLayout(annotation, ref layout);
            }

            return ClearAppearance(annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Reads a callout's target from its leader line and its text corner from its anchor.</summary>
    /// <param name="annotation">The callout.</param>
    /// <param name="layout">Receives the points it finds.</param>
    private static void ReadCalloutLayout(nint annotation, ref CalloutLayout layout)
    {
        var path = new PathBuffer();
        try
        {
            if (ReadArray(ReadString(annotation, EntriesKey), "/CL", ref path) && path.PointCount > 0)
            {
                layout.TargetX = path.Points[0].X;
                layout.TargetY = path.Points[0].Y;
            }

            path.Clear();
            if (ReadArray(ReadString(annotation, AnchorKey), "/A", ref path) && path.PointCount > 0)
            {
                layout.Left = path.Points[0].X;
                layout.Top = path.Points[0].Y;
            }
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>Creates a callout's text, frame and leader line, and works out its rectangle and where the line meets the frame.</summary>
    /// <param name="document">The document.</param>
    /// <param name="style">The font, size and colour.</param>
    /// <param name="text">The text.</param>
    /// <param name="layout">The target and text corner; receives the rectangle and the line's end.</param>
    /// <param name="objects">Receives the page objects.</param>
    /// <returns>The number of objects made.</returns>
    private static int BuildCallout(PdfiumDocumentHandle document, in TextStyle style, string text, ref CalloutLayout layout, Span<nint> objects)
    {
        PdfBounds bounds = default;
        var count = CreateTextLines(document, style, text, layout.Left + CalloutInset, layout.Top - CalloutInset, objects[..^CalloutShapes], ref bounds);
        var box = bounds.ToRect(CalloutPadding);
        var color = style.Color;
        var frame = NativeMethods.FPDFPageObj_CreateNewRect(box.Left, box.Bottom, box.Right - box.Left, box.Top - box.Bottom);
        Stroke(frame, color);
        objects[count] = frame;
        count++;

        // The line leaves the middle of the frame's side nearest the target.
        var middleX = (box.Left + box.Right) * Half;
        var middleY = (box.Top + box.Bottom) * Half;
        Span<(float X, float Y)> sides = [(box.Left, middleY), (box.Right, middleY), (middleX, box.Top), (middleX, box.Bottom)];
        var attach = sides[0];
        foreach (var side in sides)
        {
            if (Distance(side, layout.TargetX, layout.TargetY) < Distance(attach, layout.TargetX, layout.TargetY))
            {
                attach = side;
            }
        }

        layout.AttachX = attach.X;
        layout.AttachY = attach.Y;
        var angle = Math.Atan2(attach.Y - layout.TargetY, attach.X - layout.TargetX);
        var head = Math.Max(MinArrowHead, ArrowHeadWidths * CalloutLine);
        var leader = NativeMethods.FPDFPageObj_CreateNewPath(attach.X, attach.Y);
        _ = NativeMethods.FPDFPath_LineTo(leader, layout.TargetX, layout.TargetY);
        var sideA = ((float)(layout.TargetX + (Math.Cos(angle + ArrowHeadAngle) * head)), (float)(layout.TargetY + (Math.Sin(angle + ArrowHeadAngle) * head)));
        var sideB = ((float)(layout.TargetX + (Math.Cos(angle - ArrowHeadAngle) * head)), (float)(layout.TargetY + (Math.Sin(angle - ArrowHeadAngle) * head)));
        _ = NativeMethods.FPDFPath_MoveTo(leader, sideA.Item1, sideA.Item2);
        _ = NativeMethods.FPDFPath_LineTo(leader, layout.TargetX, layout.TargetY);
        _ = NativeMethods.FPDFPath_LineTo(leader, sideB.Item1, sideB.Item2);
        Stroke(leader, color);
        objects[count] = leader;
        count++;

        PdfBounds all = default;
        all.Add(box.Left, box.Bottom);
        all.Add(box.Right, box.Top);
        all.Add(layout.TargetX, layout.TargetY);
        all.Add(sideA.Item1, sideA.Item2);
        all.Add(sideB.Item1, sideB.Item2);
        layout.Rect = all.ToRect(CalloutLine);
        return count;
    }

    /// <summary>Records a callout's text, size, text corner and the standard entries it gets when saved.</summary>
    /// <param name="annotation">The callout.</param>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The text size.</param>
    /// <param name="color">The colour.</param>
    /// <param name="layout">Its layout.</param>
    private static void WriteCalloutKeys(nint annotation, string text, float fontSize, uint color, CalloutLayout layout)
    {
        _ = SetString(annotation, TextKey, text);
        _ = SetNumber(annotation, FontSizeKey, fontSize);
        var writer = new AppearanceWriter(EntriesChars);
        try
        {
            writer.Append("/A [");
            writer.Number(layout.Left);
            writer.Number(layout.Top);
            writer.Append("]");
            _ = writer.ApplyString(annotation, AnchorKey);
        }
        finally
        {
            writer.Dispose();
        }

        writer = new(EntriesChars);
        try
        {
            writer.Append("/Subtype /FreeText /IT /FreeTextCallout /LE /OpenArrow /CL [");
            writer.Number(layout.TargetX);
            writer.Number(layout.TargetY);
            writer.Number(layout.AttachX);
            writer.Number(layout.AttachY);
            writer.Append("] /DA (/Helv ");
            writer.Number(fontSize);
            writer.Append("Tf ");
            writer.Color(color, "rg)");
            _ = writer.ApplyString(annotation, EntriesKey);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Moves text objects so their bounds start at a corner, and returns the moved bounds.</summary>
    /// <param name="objects">The text objects.</param>
    /// <param name="bounds">Their bounds.</param>
    /// <param name="left">The PDF x the bounds should start at.</param>
    /// <param name="top">The PDF y of the bounds' top.</param>
    /// <returns>The moved bounds.</returns>
    private static FsRectF Align(ReadOnlySpan<nint> objects, in FsRectF bounds, float left, float top)
    {
        var dx = left - bounds.Left;
        var dy = top - bounds.Top;
        foreach (var pageObject in objects)
        {
            NativeMethods.FPDFPageObj_Transform(pageObject, 1, 0, 0, 1, dx, dy);
        }

        return new(bounds.Left + dx, bounds.Top + dy, bounds.Right + dx, bounds.Bottom + dy);
    }

    /// <summary>Moves the text corner recorded for laying a callout out again.</summary>
    /// <param name="annotation">The callout.</param>
    /// <param name="map">The move.</param>
    private static void MoveAnchor(nint annotation, in RectMap map)
    {
        var path = new PathBuffer();
        var writer = new AppearanceWriter(EntriesChars);
        try
        {
            if (!ReadArray(ReadString(annotation, AnchorKey), "/A", ref path) || path.PointCount == 0)
            {
                return;
            }

            var moved = map.Apply(path.Points[0].X, path.Points[0].Y);
            writer.Append("/A [");
            writer.Number(moved.X);
            writer.Number(moved.Y);
            writer.Append("]");
            _ = writer.ApplyString(annotation, AnchorKey);
        }
        finally
        {
            writer.Dispose();
            path.Dispose();
        }
    }

    /// <summary>Makes a path draw as a thin line in a colour.</summary>
    /// <param name="path">The path.</param>
    /// <param name="color">The colour.</param>
    private static void Stroke(nint path, uint color)
    {
        _ = NativeMethods.FPDFPath_SetDrawMode(path, 0, 1);
        _ = NativeMethods.FPDFPageObj_SetStrokeWidth(path, CalloutLine);
        _ = NativeMethods.FPDFPageObj_SetLineCap(path, RoundLine);
        _ = NativeMethods.FPDFPageObj_SetLineJoin(path, RoundLine);
        _ = NativeMethods.FPDFPageObj_SetStrokeColor(path, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);
    }

    /// <summary>Gets the distance between two points.</summary>
    /// <param name="point">The first point.</param>
    /// <param name="x">The second point's x.</param>
    /// <param name="y">The second point's y.</param>
    /// <returns>The distance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Distance((float X, float Y) point, float x, float y) => Math.Sqrt(((point.X - x) * (point.X - x)) + ((point.Y - y) * (point.Y - y)));

    /// <summary>Where a callout's parts go, in PDF space.</summary>
    /// <param name="TargetX">The x the arrow points at.</param>
    /// <param name="TargetY">The y the arrow points at.</param>
    /// <param name="Left">The x of the text's top-left corner.</param>
    /// <param name="Top">The y of the text's top-left corner.</param>
    private record struct CalloutLayout(float TargetX, float TargetY, float Left, float Top)
    {
        /// <summary>Gets or sets the x where the line meets the frame.</summary>
        public float AttachX { get; set; }

        /// <summary>Gets or sets the y where the line meets the frame.</summary>
        public float AttachY { get; set; }

        /// <summary>Gets or sets the annotation rectangle.</summary>
        public FsRectF Rect { get; set; }
    }
}
