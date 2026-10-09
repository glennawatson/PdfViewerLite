// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// Moves form values and annotations between a document and FDF or XFDF files. Importing changes the document's objects
/// in memory, so it saves as an incremental update. Callers serialise edits to one document.
/// </summary>
public static class PdfInterchange
{
    /// <summary>Reads a document's form values and annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="content">What to read.</param>
    /// <returns>The data, which can be written as FDF or XFDF.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfInterchangeData Export(PdfDocument document, PdfInterchangeContent content)
    {
        ArgumentNullException.ThrowIfNull(document);
        return InterchangeExporter.Export(document, content);
    }

    /// <summary>Exports a document's form values and annotations as XFDF.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The XFDF file's UTF-8 bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ExportXfdf(PdfDocument document) => XfdfWriter.Write(Export(document, PdfInterchangeContent.All));

    /// <summary>Exports a document as XFDF.</summary>
    /// <param name="document">The document.</param>
    /// <param name="content">What to write.</param>
    /// <returns>The XFDF file's UTF-8 bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ExportXfdf(PdfDocument document, PdfInterchangeContent content) => XfdfWriter.Write(Export(document, content));

    /// <summary>Exports a document's form values and annotations as FDF.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The FDF file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ExportFdf(PdfDocument document) => FdfWriter.Write(Export(document, PdfInterchangeContent.All));

    /// <summary>Exports a document as FDF.</summary>
    /// <param name="document">The document.</param>
    /// <param name="content">What to write.</param>
    /// <returns>The FDF file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ExportFdf(PdfDocument document, PdfInterchangeContent content) => FdfWriter.Write(Export(document, content));

    /// <summary>
    /// Applies data to a document. Field values go to fields of the same fully qualified name through the form, which
    /// redraws their appearances; unknown fields are ignored. Annotations are added to their pages, replies are linked by
    /// <see cref="PdfInterchangeAnnotation.InReplyTo"/> and <see cref="PdfInterchangeAnnotation.Name"/>, and an annotation
    /// replaces one of the same name on its page.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="data">The data.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfInterchangeImportResult Import(PdfDocument document, PdfInterchangeData data)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(data);
        return InterchangeImporter.Import(document, data);
    }

    /// <summary>Reads an XFDF file and applies it to a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="xfdf">The XFDF file's bytes.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file is not well formed XFDF.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfInterchangeImportResult ImportXfdf(PdfDocument document, byte[] xfdf) => Import(document, XfdfReader.Read(xfdf));

    /// <summary>Reads an FDF file and applies it to a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="fdf">The FDF file's bytes.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file is not FDF.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfInterchangeImportResult ImportFdf(PdfDocument document, byte[] fdf) => Import(document, FdfReader.Read(fdf));
}
