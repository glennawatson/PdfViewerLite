// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Document;

/// <content>The logical structure (tagged PDF) and the marked content each page draws, both read once on first use.</content>
public sealed partial class PdfDocument
{
    /// <summary>Stands for a document read and found to have no structure tree.</summary>
    private static readonly object NoStructureTree = new();

    /// <summary>Serialises building the structure tree, so it is built once.</summary>
    private readonly Lock _structureGate = new();

    /// <summary>The structure tree, <see cref="NoStructureTree"/>, or <see langword="null"/> before it is read.</summary>
    private object? _structureTree;

    /// <summary>Each page's marked content, recorded on first use.</summary>
    private PdfMarkedContentPage?[]? _markedContent;

    /// <summary>Gets the structure tree, read on first use; <see langword="null"/> for a document that has none.</summary>
    public PdfStructureTree? StructureTree
    {
        get
        {
            var tree = Volatile.Read(ref _structureTree);
            if (tree is null)
            {
                lock (_structureGate)
                {
                    tree = _structureTree ?? PdfStructureTree.Load(this) ?? NoStructureTree;
                    Volatile.Write(ref _structureTree, tree);
                }
            }

            return tree as PdfStructureTree;
        }
    }

    /// <summary>Gets what a page draws inside marked content: each glyph's text, box, marked content id and artifact flag.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's marked content, recorded on first use and then shared.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    public PdfMarkedContentPage GetMarkedContent(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        if (Volatile.Read(ref _markedContent) is null)
        {
            _ = Interlocked.CompareExchange(ref _markedContent, new PdfMarkedContentPage?[PageCount], null);
        }

        var pages = Volatile.Read(ref _markedContent)!;
        if (Volatile.Read(ref pages[pageIndex]) is { } recorded)
        {
            return recorded;
        }

        var page = MarkedContentRecorder.Record(this, GetPage(pageIndex));
        return Interlocked.CompareExchange(ref pages[pageIndex], page, null) ?? page;
    }
}
