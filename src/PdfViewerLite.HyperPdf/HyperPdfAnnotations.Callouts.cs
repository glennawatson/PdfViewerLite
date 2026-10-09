// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Numerics;
using System.Text.Unicode;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Callouts: text in a frame with a leader line and open arrow pointing at the page, written as a standard callout
/// (<c>/Subtype /FreeText</c>, <c>/IT /FreeTextCallout</c>, <c>/CL</c> and <c>/LE /OpenArrow</c>) with its drawn
/// appearance, so every reader shows it the same way.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>The width of a callout's frame and leader line.</summary>
    private const float CalloutLine = 1;

    /// <summary>The space between a callout's text and its frame.</summary>
    private const float CalloutPadding = 4;

    /// <summary>How far a callout's text sits inside its frame's edge.</summary>
    private const float CalloutInset = CalloutPadding + CalloutLine;

    /// <summary>The bytes the default appearance and the anchor take.</summary>
    private const int EntryBytes = 128;

    /// <summary>The points of a leader line: the target, then where it meets the frame.</summary>
    private const int LeaderPoints = 2;

    /// <summary>The largest colour channel value.</summary>
    private const float ChannelMax = 255;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>One channel's bits.</summary>
    private const uint ChannelMask = 0xFF;

    /// <inheritdoc/>
    public int AddCallout(int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text) || fontSize <= 0)
        {
            return -1;
        }

        lock (_gate)
        {
            if (GetPage(pageIndex) is not { } page)
            {
                return -1;
            }

            var callout = PdfAnnotations.Create(_store, KnownName.FreeText, default);
            callout.Set(KnownName.IT, PdfValue.FromName(_names.Callout));
            callout.Set(KnownName.LE, PdfValue.FromName(KnownName.OpenArrow));
            return BuildCallout(callout, text, new(fontSize, color), ToUser(page, target), ToUser(page, location))
                ? Add(pageIndex, page, callout, color, text, CalloutSubject)
                : -1;
        }
    }

    /// <summary>Gets where a leader line leaves a frame: the middle of the side nearest the target.</summary>
    /// <param name="box">The frame.</param>
    /// <param name="target">The target.</param>
    /// <returns>The point on the frame.</returns>
    private static Vector2 Attachment(PdfRectangle box, Vector2 target)
    {
        var middleX = (box.Left + box.Right) * Half;
        var middleY = (box.Top + box.Bottom) * Half;
        Span<Vector2> sides = [new(box.Left, middleY), new(box.Right, middleY), new(middleX, box.Top), new(middleX, box.Bottom)];
        var attach = sides[0];
        foreach (var side in sides)
        {
            if (Vector2.Distance(side, target) < Vector2.Distance(attach, target))
            {
                attach = side;
            }
        }

        return attach;
    }

    /// <summary>Lays a callout out again at a new text size, keeping its target and text corner.</summary>
    /// <param name="callout">The callout.</param>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The new size.</param>
    /// <param name="color">The colour.</param>
    private void RebuildCallout(PdfDictionary callout, string text, float fontSize, uint color)
    {
        var rectangle = PdfAnnotations.GetRectangle(callout);
        var target = new Vector2(rectangle.Left, rectangle.Bottom);
        var corner = TryReadAnchor(callout, out var anchor) ? anchor : new(rectangle.Left, rectangle.Top);
        var path = default(PdfStrokeBuffer);
        try
        {
            if (PdfAnnotations.ReadPoints(callout, KnownName.CL, ref path) > 0)
            {
                target = path.Points[0];
            }
        }
        finally
        {
            path.Dispose();
        }

        _ = BuildCallout(callout, text, new(fontSize, color), target, corner);
    }

    /// <summary>Draws a callout and records its rectangle, leader line, text, size, text corner and default appearance.</summary>
    /// <param name="callout">The callout.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The text size and colour.</param>
    /// <param name="target">The point the arrow points at, in user space.</param>
    /// <param name="corner">The text's top-left corner, in user space.</param>
    /// <returns><see langword="false"/> when no line has a character the font can show.</returns>
    private bool BuildCallout(PdfDictionary callout, string text, CalloutText style, Vector2 target, Vector2 corner)
    {
        var origin = corner + new Vector2(CalloutInset, -CalloutInset);
        var ink = MeasureLines(AppearanceFont.Helvetica, style.Size, text, origin);
        if (!ink.IsSet)
        {
            return false;
        }

        var box = ink.ToRectangle(CalloutPadding);
        var attach = Attachment(box, target);
        var (first, second) = PdfAppearances.OpenArrowHead(target, attach, ArrowHead(CalloutLine));
        var all = default(UserBounds);
        all.AddRectangle(box);
        all.Add(target);
        all.Add(first);
        all.Add(second);
        var rectangle = all.ToRectangle(CalloutLine);
        PdfAnnotations.SetRectangle(callout, rectangle);
        PdfAnnotations.SetPoints(callout, KnownName.CL, [target, attach]);
        WriteCalloutKeys(callout, text, style, corner);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            PdfAppearances.SetColors(ref builder, style.Color);
            WriteLines(ref builder, new(AppearanceFont.Helvetica, style.Size), text, origin);
            PdfAppearances.SetRoundLine(ref builder, CalloutLine);
            builder.Rectangle(box.Left, box.Bottom, box.Width, box.Height);
            builder.Stroke();
            ReadOnlySpan<Vector2> leader = [attach, target, first, target, second];
            PdfAppearances.AddStrokes(ref builder, leader, [LeaderPoints, LeaderPoints + 1]);
            builder.Stroke();
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(_store, callout, builder.ToFormXObject(_store, rectangle, CreateFontResources(AppearanceFont.Helvetica)));
        }
        finally
        {
            builder.Dispose();
        }

        return true;
    }

    /// <summary>Records a callout's text, size, text corner and default appearance.</summary>
    /// <param name="callout">The callout.</param>
    /// <param name="text">The text.</param>
    /// <param name="style">The text size and colour.</param>
    /// <param name="corner">The text's top-left corner.</param>
    private void WriteCalloutKeys(PdfDictionary callout, string text, CalloutText style, Vector2 corner)
    {
        PdfAnnotations.SetText(callout, _names.Text, text);
        PdfAnnotations.SetNumberText(callout, _names.FontSize, style.Size);
        Span<byte> buffer = stackalloc byte[EntryBytes];
        var red = ((style.Color >> RedShift) & ChannelMask) / ChannelMax;
        var green = ((style.Color >> GreenShift) & ChannelMask) / ChannelMax;
        _ = Utf8.TryWrite(buffer, CultureInfo.InvariantCulture, $"/Helv {style.Size:0.###} Tf {red:0.###} {green:0.###} {(style.Color & ChannelMask) / ChannelMax:0.###} rg", out var written);
        callout.Set(KnownName.DA, PdfValue.FromString(buffer[..written].ToArray()));
        _ = Utf8.TryWrite(buffer, CultureInfo.InvariantCulture, $"/A [{corner.X:0.###} {corner.Y:0.###} ]", out written);
        callout.Set(_names.Anchor, PdfValue.FromString(buffer[..written].ToArray()));
    }

    /// <summary>Reads a callout's text corner, kept as <c>/A [x y]</c> text the way the PDFium engine writes it.</summary>
    /// <param name="callout">The callout.</param>
    /// <param name="anchor">The corner.</param>
    /// <returns><see langword="true"/> when read.</returns>
    private bool TryReadAnchor(PdfDictionary callout, out Vector2 anchor)
    {
        anchor = default;
        var text = callout.GetStringBytes(_names.Anchor);
        var open = text.IndexOf((byte)'[');
        if (open < 0)
        {
            return false;
        }

        var numbers = text[(open + 1)..].Trim(" ]"u8);
        var space = numbers.IndexOf((byte)' ');
        if (space < 0 || !PdfNumber.TryParseSingle(numbers[..space], out var x) || !PdfNumber.TryParseSingle(numbers[(space + 1)..].Trim((byte)' '), out var y))
        {
            return false;
        }

        anchor = new(x, y);
        return true;
    }

    /// <summary>Moves a callout's leader line and text corner with it.</summary>
    /// <param name="annotation">The annotation, which may not be a callout.</param>
    /// <param name="map">The move.</param>
    private void MoveCallout(PdfDictionary annotation, in RectangleMap map)
    {
        if (!HasSubject(annotation, CalloutSubject))
        {
            return;
        }

        var path = default(PdfStrokeBuffer);
        try
        {
            if (PdfAnnotations.ReadPoints(annotation, KnownName.CL, ref path) > 0)
            {
                map.Apply(path.Points);
                PdfAnnotations.SetPoints(annotation, KnownName.CL, path.Points);
            }
        }
        finally
        {
            path.Dispose();
        }

        if (!TryReadAnchor(annotation, out var anchor))
        {
            return;
        }

        var moved = map.Apply(anchor);
        Span<byte> buffer = stackalloc byte[EntryBytes];
        _ = Utf8.TryWrite(buffer, CultureInfo.InvariantCulture, $"/A [{moved.X:0.###} {moved.Y:0.###} ]", out var written);
        annotation.Set(_names.Anchor, PdfValue.FromString(buffer[..written].ToArray()));
    }
}
