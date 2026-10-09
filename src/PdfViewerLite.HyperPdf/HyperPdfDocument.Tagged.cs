// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Structure.Tagged;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.HyperPdf;

/// <content>The tagged structure read by the managed library, in the shape the reading view takes.</content>
public sealed partial class HyperPdfDocument : ITaggedStructureSource
{
    /// <summary>The least share of the page's marked text the tags must cover to be trusted over the layout.</summary>
    private const double MinimumCoverage = 0.6;

    /// <summary>The least share of the page's text that must be marked content for the tags to be used.</summary>
    private const double MinimumMarked = 0.5;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetTaggedBlocks(int pageIndex, List<TaggedBlock> output) => GetTaggedBlocksNative(pageIndex, output);

    /// <summary>Builds a page's reading nodes from recognised words, for a page that is only an image.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words, in page space.</param>
    /// <returns>The page's nodes, each labelled as text recognition.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="words"/> is <see langword="null"/>.</exception>
    internal static PdfSemanticPage ReadRecognisedWords(int pageIndex, IReadOnlyList<OcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var converted = new PdfOcrWord[words.Count];
        for (var i = 0; i < converted.Length; i++)
        {
            var (text, bounds, confidence) = words[i];
            converted[i] = new(text, new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), confidence);
        }

        return PdfReadingStructure.FromOcrWords(pageIndex, converted);
    }

    /// <summary>
    /// Gets a page's block-level elements in logical order from the managed structure reader, matching
    /// <see cref="ITaggedStructureSource.GetTaggedBlocks"/> on the PDFium engine: the same coverage checks, kinds,
    /// levels and replacement text, with characters indexed as <see cref="ITextLayoutSource.GetCharacters"/> gives them.
    /// </summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="output">Receives the elements.</param>
    /// <returns><see langword="true"/> when the page's tags cover its text well enough to be trusted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    internal bool GetTaggedBlocksNative(int pageIndex, List<TaggedBlock> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (IsDisposed || (uint)pageIndex >= (uint)PageCount || HyperPdfLibrary.Document.PdfDocumentTagged.GetStructureTree(_document) is not { IsMarked: true })
        {
            return false;
        }

        var page = PdfReadingStructure.ReadTagged(_document, pageIndex);
        var content = page.Content!;
        var marked = content.MarkedCharacterCount;
        if (marked == 0 || marked < content.VisibleCharacterCount * MinimumMarked)
        {
            return false;
        }

        var characters = new List<PageCharacter>();
        GetCharacters(pageIndex, characters);
        var walker = new TaggedBlockWalker(TaggedCharacterMap.Create(content, characters));
        foreach (var node in page.Nodes)
        {
            walker.Visit(node);
        }

        if (walker.Covered < marked * MinimumCoverage || walker.Blocks.Count == 0)
        {
            return false;
        }

        output.AddRange(walker.Blocks);
        return true;
    }

    /// <summary>Reads a page's reading structure: tagged when it can be, otherwise inferred from the layout.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's nodes.</returns>
    internal PdfSemanticPage ReadReadingStructure(int pageIndex)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return PdfReadingStructure.Read(_document, pageIndex);
    }
}
