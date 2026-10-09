// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// One annotation as XFDF and FDF carry it: the entries a reader needs to recreate it, without appearance streams.
/// Both formats read into this shape and write from it.
/// </summary>
[DebuggerDisplay("PdfInterchangeAnnotation: {Subtype} page {Page} {Name}")]
public sealed class PdfInterchangeAnnotation
{
    /// <summary>Initializes a new instance of the <see cref="PdfInterchangeAnnotation"/> class.</summary>
    /// <param name="subtype">The PDF annotation subtype, such as <c>Highlight</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="subtype"/> is <see langword="null"/>.</exception>
    public PdfInterchangeAnnotation(string subtype)
    {
        ArgumentNullException.ThrowIfNull(subtype);
        Subtype = subtype;
    }

    /// <summary>Gets the PDF annotation subtype.</summary>
    public string Subtype { get; }

    /// <summary>Gets or sets the zero based page index.</summary>
    public int Page { get; set; }

    /// <summary>Gets or sets the rectangle, or <see langword="null"/> when the file gives none.</summary>
    public PdfRectangle? Rect { get; set; }

    /// <summary>Gets or sets the colour as 0xRRGGBB, or <see langword="null"/>.</summary>
    public uint? Color { get; set; }

    /// <summary>Gets or sets the interior colour as 0xRRGGBB, or <see langword="null"/>.</summary>
    public uint? InteriorColor { get; set; }

    /// <summary>Gets or sets the annotation flags.</summary>
    public PdfAnnotationFlags Flags { get; set; }

    /// <summary>Gets or sets the unique name (<c>/NM</c>) that replies refer to, or <see langword="null"/>.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the author (<c>/T</c>), or <see langword="null"/>.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the subject (<c>/Subj</c>), or <see langword="null"/>.</summary>
    public string? Subject { get; set; }

    /// <summary>Gets or sets the text (<c>/Contents</c>), or <see langword="null"/>.</summary>
    public string? Contents { get; set; }

    /// <summary>Gets or sets the rich text (<c>/RC</c>, XHTML), or <see langword="null"/>.</summary>
    public string? RichContents { get; set; }

    /// <summary>Gets or sets the modification date, or <see langword="null"/>.</summary>
    public DateTimeOffset? Date { get; set; }

    /// <summary>Gets or sets the creation date, or <see langword="null"/>.</summary>
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary>Gets or sets the constant opacity from 0 to 1, or <see langword="null"/>.</summary>
    public float? Opacity { get; set; }

    /// <summary>Gets or sets the border width, or <see langword="null"/>.</summary>
    public float? Width { get; set; }

    /// <summary>Gets or sets the border style letter: S, D, B, I or U, or <see langword="null"/>.</summary>
    public string? Style { get; set; }

    /// <summary>Gets or sets the dash pattern; empty when there is none.</summary>
    public ReadOnlyMemory<float> Dashes { get; set; }

    /// <summary>Gets or sets the cloud intensity, or <see langword="null"/> for no cloud.</summary>
    public float? Intensity { get; set; }

    /// <summary>Gets or sets the name of the annotation this one answers, or <see langword="null"/>.</summary>
    public string? InReplyTo { get; set; }

    /// <summary>Gets or sets the relationship to the annotation it answers: <c>R</c> or <c>Group</c>, or <see langword="null"/>.</summary>
    public string? ReplyType { get; set; }

    /// <summary>Gets or sets the review or marked state, or <see langword="null"/>.</summary>
    public string? State { get; set; }

    /// <summary>Gets or sets the state model, <c>Review</c> or <c>Marked</c>, or <see langword="null"/>.</summary>
    public string? StateModel { get; set; }

    /// <summary>Gets or sets the icon name, or <see langword="null"/>.</summary>
    public string? Icon { get; set; }

    /// <summary>Gets or sets the caret symbol, <c>P</c> or <c>None</c>, or <see langword="null"/>.</summary>
    public string? Symbol { get; set; }

    /// <summary>Gets or sets a value indicating whether a text note shows its pop-up open, or <see langword="null"/>.</summary>
    public bool? IsOpen { get; set; }

    /// <summary>Gets or sets the quadrilaterals of a text markup annotation (<c>/QuadPoints</c>); empty when there are none.</summary>
    public ReadOnlyMemory<float> Coords { get; set; }

    /// <summary>Gets or sets the points of a polygon or polyline (<c>/Vertices</c>); empty when there are none.</summary>
    public ReadOnlyMemory<float> Vertices { get; set; }

    /// <summary>Gets or sets the end points of a line, <c>x1 y1 x2 y2</c> (<c>/L</c>); empty when there is no line.</summary>
    public ReadOnlyMemory<float> Line { get; set; }

    /// <summary>Gets or sets the callout line of a free text annotation (<c>/CL</c>); empty when there is none.</summary>
    public ReadOnlyMemory<float> Callout { get; set; }

    /// <summary>Gets or sets the margins between the rectangle and the text (<c>/RD</c>); empty when there are none.</summary>
    public ReadOnlyMemory<float> Fringe { get; set; }

    /// <summary>Gets or sets the strokes of an ink annotation, each a flat <c>x y x y</c> array; empty when there are none.</summary>
    public ReadOnlyMemory<ReadOnlyMemory<float>> Gestures { get; set; }

    /// <summary>Gets or sets the line ending at the start of a line, or <see langword="null"/>.</summary>
    public string? Head { get; set; }

    /// <summary>Gets or sets the line ending at the end of a line, or <see langword="null"/>.</summary>
    public string? Tail { get; set; }

    /// <summary>Gets or sets the default appearance string (<c>/DA</c>), or <see langword="null"/>.</summary>
    public string? DefaultAppearance { get; set; }

    /// <summary>Gets or sets the default style string for rich text (<c>/DS</c>), or <see langword="null"/>.</summary>
    public string? DefaultStyle { get; set; }

    /// <summary>Gets or sets the text alignment (<c>/Q</c>): 0 left, 1 centre, 2 right, or <see langword="null"/>.</summary>
    public int? Justification { get; set; }

    /// <summary>Gets or sets the attached file of a file attachment annotation, or <see langword="null"/>.</summary>
    public PdfInterchangeAttachment? Attachment { get; set; }

    /// <summary>Gets or sets the pop-up window, or <see langword="null"/>.</summary>
    public PdfInterchangePopup? Popup { get; set; }
}
