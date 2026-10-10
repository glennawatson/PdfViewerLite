// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>A generated PDF opened with HyperPDF, whose annotations are edited natively; deleted on dispose.</summary>
internal sealed class NativeDocument : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="NativeDocument"/> class.</summary>
    /// <param name="pageCount">The page count.</param>
    internal NativeDocument(int pageCount)
        : this(TestPdf.Create(pageCount))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NativeDocument"/> class.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <exception cref="InvalidOperationException">The document has no text layer writer.</exception>
    internal NativeDocument(byte[] bytes)
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"hyperpdf-annotations-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(FilePath, bytes);
        Document = (HyperPdfDocument)new HyperPdfEngine().Open(FilePath, null);
        Editor = HyperPdfAnnotationStateAccess.GetAnnotations(Document);
    }

    /// <summary>Gets the file path.</summary>
    internal string FilePath { get; }

    /// <summary>Gets the document.</summary>
    internal HyperPdfDocument Document { get; }

    /// <summary>Gets the native editor.</summary>
    internal HyperPdfAnnotations Editor { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Document.Dispose();
        File.Delete(FilePath);
    }

    /// <summary>Saves an editor's document into memory.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The saved bytes.</returns>
    internal static byte[] Save(IAnnotationEditor editor)
    {
        using var stream = new MemoryStream();
        _ = editor.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Saves an editor's document into memory.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The saved bytes.</returns>
    internal static byte[] Save(HyperPdfAnnotations editor)
    {
        using var stream = new MemoryStream();
        _ = HyperPdfAnnotationSaving.Save(editor, stream);
        return stream.ToArray();
    }

    /// <summary>Reads a page's annotations from a file.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations.</returns>
    internal static List<PageAnnotation> Read(IAnnotationEditor editor, int page)
    {
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(page, annotations);
        return annotations;
    }

    /// <summary>Reads a page's annotations from a file.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations.</returns>
    internal static List<PageAnnotation> Read(HyperPdfAnnotations editor, int page)
    {
        var annotations = new List<PageAnnotation>();
        HyperPdfAnnotationReading.GetAnnotations(editor, page, annotations);
        return annotations;
    }

    /// <summary>Opens saved bytes with PDFium and reads a page's annotations.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations.</returns>
    internal static List<PageAnnotation> ReadWithPdfium(byte[] bytes, int page)
    {
        using var document = OpenWithPdfium(bytes, out var path);
        try
        {
            return Read((IAnnotationEditor)DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!, page);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Opens saved bytes with PDFium; the caller deletes the file.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="path">Receives the path.</param>
    /// <returns>The document.</returns>
    internal static IDocument OpenWithPdfium(byte[] bytes, out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"hyperpdf-saved-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        var document = new PdfiumEngine().Open(path, null);
        ((PdfiumDocument)document).FontCatalog = TestFont.Catalog;
        return document;
    }

    /// <summary>Reads the annotation dictionaries of a page of saved bytes with the managed library.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="page">The page.</param>
    /// <returns>The dictionaries.</returns>
    internal static List<PdfDictionary> Dictionaries(byte[] bytes, int page)
    {
        var document = PdfDocumentReader.Open(bytes, null);
        var result = new List<PdfDictionary>();
        if (PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, page)) is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                result.Add(array.GetDictionary(i)!);
            }
        }

        return result;
    }

    /// <summary>Gets a name's spelling in a dictionary's document.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The name, or empty.</returns>
    internal static string Name(PdfDictionary dictionary, KnownName key) => dictionary.Owner!.Names.GetString(dictionary.GetName(key));
}
