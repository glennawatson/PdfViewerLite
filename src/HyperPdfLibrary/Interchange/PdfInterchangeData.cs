// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// The content of an FDF or XFDF file: form field values and annotations, with the file's details. Both formats read
/// into this shape and write from it, so a file converts between them by reading one and writing the other.
/// </summary>
[DebuggerDisplay("PdfInterchangeData: {Fields.Count} fields, {Annotations.Count} annotations")]
public sealed class PdfInterchangeData
{
    /// <summary>Gets the form field values.</summary>
    public List<PdfInterchangeField> Fields { get; } = [];

    /// <summary>Gets the annotations.</summary>
    public List<PdfInterchangeAnnotation> Annotations { get; } = [];

    /// <summary>Gets or sets the PDF file the data belongs to (<c>f href</c> or <c>/F</c>), or <see langword="null"/>.</summary>
    public string? FileHref { get; set; }

    /// <summary>Gets or sets the first file identifier as hexadecimal (<c>ids original</c>), or <see langword="null"/>.</summary>
    public string? OriginalId { get; set; }

    /// <summary>Gets or sets the second file identifier as hexadecimal (<c>ids modified</c>), or <see langword="null"/>.</summary>
    public string? ModifiedId { get; set; }

    /// <summary>Gets or sets the FDF status message (<c>/Status</c>), or <see langword="null"/>.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the FDF text encoding name (<c>/Encoding</c>), or <see langword="null"/>. Text is read as PDF text strings whatever it is.</summary>
    public string? Encoding { get; set; }

    /// <summary>Gets or sets the FDF scripts, kept as data and never run, or <see langword="null"/>.</summary>
    public PdfInterchangeScripts? JavaScript { get; set; }

    /// <summary>Gets or sets the decoded FDF page differences stream (<c>/Differences</c>); empty when there is none.</summary>
    public ReadOnlyMemory<byte> Differences { get; set; }
}
