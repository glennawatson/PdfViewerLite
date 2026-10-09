// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// What an annotation is and what colour it shows, read the way the PDFium engine reads them: subtypes, then the
/// <c>/Subj</c> this viewer writes to tell its signatures, arrows, lines, stamps, text boxes, callouts and polygons apart.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>The colour PDF readers give a highlight without one.</summary>
    private const uint DefaultHighlight = 0xFFFF00;

    /// <summary>The colour PDF readers give other annotations without one.</summary>
    private const uint DefaultColor = 0;

    /// <summary>Gets the subject marking a drawn or typed signature.</summary>
    private static ReadOnlySpan<byte> SignatureSubject => "Signature"u8;

    /// <summary>Gets the subject marking text written on the page.</summary>
    private static ReadOnlySpan<byte> TextBoxSubject => "Text box"u8;

    /// <summary>Gets the subject marking an arrow drawn as ink.</summary>
    private static ReadOnlySpan<byte> ArrowSubject => "Arrow"u8;

    /// <summary>Gets the subject marking a line drawn as ink.</summary>
    private static ReadOnlySpan<byte> LineSubject => "Line"u8;

    /// <summary>Gets the subject marking a stamp this viewer placed.</summary>
    private static ReadOnlySpan<byte> StampSubject => "Stamp"u8;

    /// <summary>Gets the subject marking a callout.</summary>
    private static ReadOnlySpan<byte> CalloutSubject => "Callout"u8;

    /// <summary>Gets the subject marking a polygon.</summary>
    private static ReadOnlySpan<byte> PolygonSubject => "Polygon"u8;

    /// <summary>Gets the subject marking a cloud.</summary>
    private static ReadOnlySpan<byte> CloudSubject => "Cloud"u8;

    /// <summary>Gets the subject marking a run of lines.</summary>
    private static ReadOnlySpan<byte> PolyLineSubject => "Polyline"u8;

    /// <summary>Maps the subtypes that need no subject to a kind.</summary>
    /// <param name="subtype">The subtype.</param>
    /// <returns>The kind.</returns>
    private static AnnotationKind SimpleKind(KnownName subtype) => subtype switch
    {
        KnownName.Text => AnnotationKind.Note,
        KnownName.Highlight => AnnotationKind.Highlight,
        KnownName.Underline => AnnotationKind.Underline,
        KnownName.Squiggly => AnnotationKind.Squiggly,
        KnownName.StrikeOut => AnnotationKind.StrikeOut,
        KnownName.PolyLine => AnnotationKind.PolyLine,
        KnownName.Square => AnnotationKind.Rectangle,
        KnownName.Circle => AnnotationKind.Ellipse,
        KnownName.Redact => AnnotationKind.Redaction,
        _ => AnnotationKind.Other,
    };

    /// <summary>Gets an annotation's kind, or <see langword="null"/> for links, form fields and pop-ups, which are not listed.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The kind.</returns>
    private AnnotationKind? GetKind(PdfDictionary annotation)
    {
        var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
        return subtype switch
        {
            KnownName.Link or KnownName.Popup or KnownName.Widget => null,
            KnownName.FreeText => HasSubject(annotation, CalloutSubject) ? AnnotationKind.Callout : AnnotationKind.TextBox,
            KnownName.Polygon => GetSavedPolygonKind(annotation),
            KnownName.Ink => GetInkKind(annotation),
            KnownName.Stamp => GetStampKind(annotation),
            _ => SimpleKind(subtype),
        };
    }

    /// <summary>Tells signatures, stamps, callouts and text boxes written here apart from other stamps.</summary>
    /// <param name="annotation">The stamp.</param>
    /// <returns>The kind.</returns>
    private AnnotationKind GetStampKind(PdfDictionary annotation)
    {
        if (HasSubject(annotation, SignatureSubject))
        {
            return AnnotationKind.Signature;
        }

        if (HasSubject(annotation, StampSubject))
        {
            return AnnotationKind.Stamp;
        }

        if (HasSubject(annotation, CalloutSubject))
        {
            return AnnotationKind.Callout;
        }

        return HasSubject(annotation, TextBoxSubject) ? AnnotationKind.TextBox : AnnotationKind.Other;
    }

    /// <summary>Tells signatures, arrows, lines and polygons drawn as ink apart from freehand ink.</summary>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>The kind.</returns>
    private AnnotationKind GetInkKind(PdfDictionary annotation)
    {
        if (HasSubject(annotation, SignatureSubject))
        {
            return AnnotationKind.Signature;
        }

        if (HasSubject(annotation, ArrowSubject))
        {
            return AnnotationKind.Arrow;
        }

        return HasSubject(annotation, LineSubject) ? AnnotationKind.Line : GetPolygonKind(annotation) ?? AnnotationKind.Ink;
    }

    /// <summary>Tells polygons, clouds and runs of lines drawn as ink apart from freehand ink.</summary>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>The kind, or <see langword="null"/> for plain ink.</returns>
    private AnnotationKind? GetPolygonKind(PdfDictionary annotation)
    {
        if (HasSubject(annotation, PolygonSubject))
        {
            return AnnotationKind.Polygon;
        }

        if (HasSubject(annotation, CloudSubject))
        {
            return AnnotationKind.Cloud;
        }

        return HasSubject(annotation, PolyLineSubject) ? AnnotationKind.PolyLine : null;
    }

    /// <summary>Tells a cloud from a plain polygon: by the subject written here, or the cloud intent.</summary>
    /// <param name="annotation">The polygon.</param>
    /// <returns>The kind.</returns>
    private AnnotationKind GetSavedPolygonKind(PdfDictionary annotation) =>
        HasSubject(annotation, CloudSubject) || annotation.GetName(KnownName.IT) == _names.PolygonCloud ? AnnotationKind.Cloud : AnnotationKind.Polygon;

    /// <summary>Determines whether an annotation's <c>/Subj</c> equals a value.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subject">The subject.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasSubject(PdfDictionary annotation, ReadOnlySpan<byte> subject) => PdfAnnotations.TextEquals(annotation, _names.Subject, subject);

    /// <summary>
    /// Gets the colour an annotation shows: its <c>/C</c>; else the first colour its appearance draws with; else the
    /// colour PDF readers default to.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns>The colour as 0xRRGGBB.</returns>
    private uint GetColor(PdfDictionary annotation, AnnotationKind kind)
    {
        if (PdfAnnotations.TryGetColor(annotation, KnownName.C, out var color))
        {
            return color;
        }

        if (PdfAnnotations.GetNormalAppearance(annotation) is not { } appearance)
        {
            return annotation.IsName(KnownName.Subtype, KnownName.Highlight) ? DefaultHighlight : DefaultColor;
        }

        if (PdfAppearanceColors.TryReadFirstColor(appearance, _store.Names, out color))
        {
            return color;
        }

        return kind is AnnotationKind.Signature or AnnotationKind.TextBox ? AnnotationColors.Ink : AnnotationColors.Sand;
    }

    /// <summary>Determines whether an annotation is removed but kept.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsRemoved(PdfDictionary annotation) => PdfAnnotations.HasText(annotation, _names.Removed);

    /// <summary>Determines whether an annotation is a reply, so it is listed with its comment rather than on its own.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> for a reply.</returns>
    private bool IsReply(PdfDictionary annotation) =>
        PdfAnnotations.HasText(annotation, _names.PendingReply) || PdfAnnotations.GetInReplyTo(annotation) is not null;

    /// <summary>Determines whether an annotation may be moved or restyled: listed, not removed, not a reply and not text markup.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns><see langword="true"/> when it may be changed.</returns>
    private bool IsEditable(PdfDictionary annotation, AnnotationKind kind) =>
        kind is not (AnnotationKind.Highlight or AnnotationKind.Underline or AnnotationKind.StrikeOut or AnnotationKind.Squiggly)
        && !IsRemoved(annotation)
        && !IsReply(annotation);
}
