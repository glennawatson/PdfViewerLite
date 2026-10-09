// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// The data a submit-form action would send. The library only builds this; it never sends it. The host decides whether
/// to send, and must ask the user first.
/// </summary>
/// <param name="Url">The address the action names, or <see langword="null"/>.</param>
/// <param name="Flags">The action's <c>/Flags</c>.</param>
/// <param name="Fields">The fields to send, in page order.</param>
[DebuggerDisplay("PdfFormSubmission: {Url}, {Fields.Length} fields")]
public sealed record PdfFormSubmission(string? Url, int Flags, PdfSubmittedField[] Fields)
{
    /// <summary>The flag that asks for HTML form encoding instead of FDF.</summary>
    private const int ExportFormatFlag = 1 << 2;

    /// <summary>The flag that asks for the HTTP GET method.</summary>
    private const int GetMethodFlag = 1 << 3;

    /// <summary>The flag that asks for XFDF instead of FDF.</summary>
    private const int XfdfFlag = 1 << 5;

    /// <summary>The flag that asks for the whole PDF file.</summary>
    private const int SubmitPdfFlag = 1 << 8;

    /// <summary>Gets a value indicating whether the data is sent as HTML form fields.</summary>
    public bool IsHtml => (Flags & ExportFormatFlag) != 0;

    /// <summary>Gets a value indicating whether the data is sent with the HTTP GET method.</summary>
    public bool IsGet => (Flags & GetMethodFlag) != 0;

    /// <summary>Gets a value indicating whether the data is sent as XFDF.</summary>
    public bool IsXfdf => (Flags & XfdfFlag) != 0;

    /// <summary>Gets a value indicating whether the action asks for the whole PDF file instead of field data.</summary>
    public bool IsPdf => (Flags & SubmitPdfFlag) != 0;
}
