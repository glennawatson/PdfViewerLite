// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Document;

/// <summary>Reads tagged content and structure trees.</summary>
public static class PdfDocumentTagged
{
    /// <summary>Stands for a document read and found to have no structure tree.</summary>
    private static readonly object NoStructureTree = new();

    /// <summary>Gets the structure tree, read on first use; <see langword="null"/> for a document that has none.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The structure tree, or null when the document has none.</returns>
    public static PdfStructureTree? GetStructureTree(PdfDocument document)
    {
        var tree = Volatile.Read(ref document.State.StructureTree);
        if (tree is null)
        {
            lock (document.State.StructureGate)
            {
                tree = document.State.StructureTree ?? PdfStructureTree.Load(document) ?? PdfDocumentTagged.NoStructureTree;
                Volatile.Write(ref document.State.StructureTree, tree);
            }
        }

        return tree as PdfStructureTree;
    }

    /// <summary>Gets what a page draws inside marked content: each glyph's text, box, marked content id and artifact flag.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's marked content, recorded on first use and then shared.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    public static PdfMarkedContentPage GetMarkedContent(PdfDocument document, int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);
        if (Volatile.Read(ref document.State.MarkedContent) is null)
        {
            _ = Interlocked.CompareExchange(ref document.State.MarkedContent, new PdfMarkedContentPage?[document.PageCount], null);
        }

        var pages = Volatile.Read(ref document.State.MarkedContent)!;
        if (Volatile.Read(ref pages[pageIndex]) is { } recorded)
        {
            return recorded;
        }

        var page = MarkedContentRecorder.Record(document, PdfDocumentPages.GetPage(document, pageIndex));
        return Interlocked.CompareExchange(ref pages[pageIndex], page, null) ?? page;
    }
}
