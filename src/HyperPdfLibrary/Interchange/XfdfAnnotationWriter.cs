// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml;
using HyperPdfLibrary.Annotations;

namespace HyperPdfLibrary.Interchange;

/// <summary>Writes one annotation element of an XFDF file.</summary>
internal static class XfdfAnnotationWriter
{
    /// <summary>The numbers in a line's end points.</summary>
    private const int LineNumbers = 4;

    /// <summary>The numbers in a point.</summary>
    private const int PointNumbers = 2;

    /// <summary>The attributes in the order they are written.</summary>
    private static readonly AttributeWriter[] Attributes =
    [
        new("page", static a => a.Page.ToString(CultureInfo.InvariantCulture)),
        new("rect", static a => a.Rect is { } rectangle ? InterchangeValues.FormatRect(rectangle) : null),
        new("color", static a => a.Color is { } color ? InterchangeValues.FormatColor(color) : null),
        new("interior-color", static a => a.InteriorColor is { } color ? InterchangeValues.FormatColor(color) : null),
        new("flags", static a => a.Flags == PdfAnnotationFlags.None ? null : InterchangeValues.FormatFlags(a.Flags)),
        new("name", static a => a.Name),
        new("title", static a => a.Title),
        new("subject", static a => a.Subject),
        new("date", static a => a.Date is { } date ? InterchangeValues.FormatDate(date) : null),
        new("creationdate", static a => a.CreationDate is { } date ? InterchangeValues.FormatDate(date) : null),
        new("opacity", static a => a.Opacity is { } opacity ? InterchangeValues.FormatNumber(opacity) : null),
        new("width", static a => a.Width is { } width ? InterchangeValues.FormatNumber(width) : null),
        new("style", static a => InterchangeValues.FormatStyle(a.Style)),
        new("dashes", static a => FormatNumbers(a.Dashes)),
        new("intensity", static a => a.Intensity is { } intensity ? InterchangeValues.FormatNumber(intensity) : null),
        new("inreplyto", static a => a.InReplyTo),
        new("replyType", static a => a.InReplyTo is null ? null : a.ReplyType),
        new("state", static a => a.State),
        new("statemodel", static a => a.StateModel),
        new("icon", static a => a.Icon),
        new("symbol", static a => a.Symbol),
        new("open", static a => a.IsOpen is { } open ? InterchangeValues.FormatBoolean(open) : null),
        new("coords", static a => FormatNumbers(a.Coords)),
        new("start", static a => FormatLineEnd(a.Line, 0)),
        new("end", static a => FormatLineEnd(a.Line, PointNumbers)),
        new("head", static a => a.Head),
        new("tail", static a => a.Tail),
        new("justification", static a => a.Justification?.ToString(CultureInfo.InvariantCulture)),
        new("callout", static a => FormatNumbers(a.Callout)),
        new("fringe", static a => FormatNumbers(a.Fringe)),
        new("file", static a => a.Attachment?.FileName),
        new("description", static a => a.Attachment?.Description),
    ];

    /// <summary>Writes an annotation. A subtype without an element is left out.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="annotation">The annotation.</param>
    internal static void Write(XmlWriter writer, PdfInterchangeAnnotation annotation)
    {
        if (!XfdfNames.TryGetElement(annotation.Subtype, out var element))
        {
            return;
        }

        writer.WriteStartElement(element);
        foreach (var (name, format) in Attributes)
        {
            XfdfWriter.WriteAttribute(writer, name, format(annotation));
        }

        WriteTexts(writer, annotation);
        WritePopup(writer, annotation);
        WriteShapes(writer, annotation);
        WriteAttachment(writer, annotation);
        writer.WriteEndElement();
    }

    /// <summary>Formats one end of a line as <c>x,y</c>.</summary>
    /// <param name="line">The line's four numbers, or <see langword="null"/>.</param>
    /// <param name="offset">0 for the start, 2 for the end.</param>
    /// <returns>The text, or <see langword="null"/> when there is no line.</returns>
    private static string? FormatLineEnd(ReadOnlyMemory<float> line, int offset) =>
        line.Length >= LineNumbers ? InterchangeValues.FormatList(line.Span.Slice(offset, PointNumbers), ',') : null;

    /// <summary>Formats numbers separated by commas.</summary>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The text, or <see langword="null"/> when there are none.</returns>
    private static string? FormatNumbers(ReadOnlyMemory<float> numbers) =>
        numbers.IsEmpty ? null : InterchangeValues.FormatList(numbers.Span, ',');

    /// <summary>Writes the text children.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="annotation">The annotation.</param>
    private static void WriteTexts(XmlWriter writer, PdfInterchangeAnnotation annotation)
    {
        WriteElement(writer, XfdfNames.Contents, annotation.Contents);
        XfdfWriter.WriteRichText(writer, XfdfNames.ContentsRichText, annotation.RichContents);
        WriteElement(writer, XfdfNames.DefaultAppearance, annotation.DefaultAppearance);
        WriteElement(writer, XfdfNames.DefaultStyle, annotation.DefaultStyle);
    }

    /// <summary>Writes the pop-up child.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="annotation">The annotation.</param>
    private static void WritePopup(XmlWriter writer, PdfInterchangeAnnotation annotation)
    {
        if (annotation.Popup is not { } popup)
        {
            return;
        }

        writer.WriteStartElement(XfdfNames.Popup);
        writer.WriteAttributeString("page", popup.Page.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("rect", InterchangeValues.FormatRect(popup.Rect));
        writer.WriteAttributeString("open", InterchangeValues.FormatBoolean(popup.IsOpen));
        writer.WriteEndElement();
    }

    /// <summary>Writes the ink strokes and vertices.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="annotation">The annotation.</param>
    private static void WriteShapes(XmlWriter writer, PdfInterchangeAnnotation annotation)
    {
        if (!annotation.Gestures.IsEmpty)
        {
            writer.WriteStartElement(XfdfNames.InkList);
            foreach (var stroke in annotation.Gestures.Span)
            {
                writer.WriteElementString(XfdfNames.Gesture, InterchangeValues.FormatList(stroke.Span, ',', ';'));
            }

            writer.WriteEndElement();
        }

        if (!annotation.Vertices.IsEmpty)
        {
            writer.WriteElementString(XfdfNames.Vertices, InterchangeValues.FormatList(annotation.Vertices.Span, ',', ';'));
        }
    }

    /// <summary>Writes the attached file as hexadecimal.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="annotation">The annotation.</param>
    private static void WriteAttachment(XmlWriter writer, PdfInterchangeAnnotation annotation)
    {
        if (annotation.Attachment is not { } attachment)
        {
            return;
        }

        writer.WriteStartElement(XfdfNames.Data);
        writer.WriteAttributeString("mode", "raw");
        writer.WriteAttributeString("encoding", "hex");
        writer.WriteAttributeString("length", attachment.Data.Length.ToString(CultureInfo.InvariantCulture));
        XfdfWriter.WriteAttribute(writer, "MIME-type", attachment.MimeType);
        writer.WriteString(Convert.ToHexString(attachment.Data));
        writer.WriteEndElement();
    }

    /// <summary>Writes a text element when there is text.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The element's name.</param>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    private static void WriteElement(XmlWriter writer, string name, string? text)
    {
        if (text is not null)
        {
            writer.WriteElementString(name, InterchangeValues.MakeXmlSafe(text));
        }
    }

    /// <summary>An attribute's name and how it is formatted.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Format">Formats the value; returns <see langword="null"/> when the annotation has none.</param>
    private readonly record struct AttributeWriter(string Name, Func<PdfInterchangeAnnotation, string?> Format);
}
