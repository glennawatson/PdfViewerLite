// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>The pages of a document as read at one moment, with their indexes by object number. Never changed once built.</summary>
[DebuggerDisplay("PdfPageSet: {Pages.Length} pages")]
internal sealed class PdfPageSet
{
    /// <summary>Initializes a new instance of the <see cref="PdfPageSet"/> class.</summary>
    /// <param name="pages">The pages, in order.</param>
    private PdfPageSet(PdfPage[] pages)
    {
        Pages = pages;
        Links = new PdfLink[]?[pages.Length];
        Indexes = [with(pages.Length)];
        foreach (var page in pages)
        {
            if (page.Id.IsValid)
            {
                _ = Indexes.TryAdd(page.Id.Number, page.Index);
            }
        }
    }

    /// <summary>Gets the pages, in order.</summary>
    internal PdfPage[] Pages { get; }

    /// <summary>Gets the link annotations read so far, by page index. Lives and dies with this set, so it always matches <see cref="Pages"/>.</summary>
    internal PdfLink[]?[] Links { get; }

    /// <summary>Gets the page indexes by page object number; a page listed twice keeps its first index.</summary>
    internal Dictionary<int, int> Indexes { get; }

    /// <summary>Reads the page tree.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The pages.</returns>
    internal static PdfPageSet Read(PdfObjectStore objects) => new(PageTreeReader.Read(objects));
}
