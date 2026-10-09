// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Flattens the page tree into an array of pages, depth first in document order, carrying inherited attributes down.
/// Loops and over-deep trees are cut off; when the tree yields no pages, the file is scanned for page objects instead.
/// </summary>
internal static class PageTreeReader
{
    /// <summary>Reads every page.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The pages.</returns>
    internal static PdfPage[] Read(PdfObjectStore objects)
    {
        var pages = new List<PdfPage>();
        var root = objects.Catalog.GetRaw(KnownName.Pages);
        var visited = new HashSet<int>();
        var pending = new Stack<PendingNode>();
        pending.Push(new(root, default, 0));
        while (pending.Count > 0)
        {
            PdfOpenContext.ThrowIfCancelled(objects.Context);
            Visit(objects, pending.Pop(), pending, visited, pages);
        }

        return pages.Count > 0 ? [.. pages] : ScanForPages(objects);
    }

    /// <summary>Visits one node: adds a page, or queues a node's kids in order.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="node">The node.</param>
    /// <param name="pending">The nodes still to visit.</param>
    /// <param name="visited">The object numbers already visited.</param>
    /// <param name="pages">The pages found.</param>
    private static void Visit(PdfObjectStore objects, in PendingNode node, Stack<PendingNode> pending, HashSet<int> visited, List<PdfPage> pages)
    {
        var id = node.Reference.AsReference();
        var dictionary = objects.Resolve(node.Reference).AsDictionary();
        if (dictionary is null)
        {
            return;
        }

        var kids = dictionary.GetArray(KnownName.Kids);
        if (kids is null || dictionary.IsName(KnownName.Type, KnownName.Page))
        {
            pages.Add(new(pages.Count, id, dictionary, node.Inherited));
            return;
        }

        // A page listed twice is listed twice, as in PDFium; only intermediate nodes are guarded against loops.
        if (node.Depth >= PdfLimits.MaxPageTreeDepth)
        {
            PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.RecursionLimit, "The page tree was too deep and was cut off.", id.Number, -1);
            return;
        }

        if (id.IsValid && !visited.Add(id.Number))
        {
            PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.RecursionLimit, "The page tree loops back on itself; the repeat was ignored.", id.Number, -1);
            return;
        }

        var inherited = node.Inherited.With(dictionary);

        // Pushed in reverse, so the first kid is visited first.
        for (var i = kids.Count - 1; i >= 0; i--)
        {
            pending.Push(new(kids.GetRaw(i), inherited, node.Depth + 1));
        }
    }

    /// <summary>Finds page objects by scanning every object, for files whose page tree is broken.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The pages, in object order.</returns>
    private static PdfPage[] ScanForPages(PdfObjectStore objects)
    {
        var pages = new List<PdfPage>();
        for (var number = 1; number < objects.Size; number++)
        {
            PdfOpenContext.ThrowIfCancelled(objects.Context);
            var id = new PdfObjectId(number, 0);
            if (objects.GetObject(id).AsDictionary() is { } dictionary && dictionary.IsName(KnownName.Type, KnownName.Page))
            {
                pages.Add(new(pages.Count, id, dictionary, InheritFromParents(dictionary)));
            }
        }

        if (pages.Count > 0)
        {
            PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.PageTreeRebuilt, "The page tree was missing or broken; the pages were found by scanning.", 0, -1);
        }

        return [.. pages];
    }

    /// <summary>Collects the attributes a page inherits by walking its /Parent chain; loops and over-deep chains are cut off.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The inherited attributes.</returns>
    private static InheritedAttributes InheritFromParents(PdfDictionary page)
    {
        var parent = page.GetDictionary(KnownName.Parent);
        if (parent is null)
        {
            return default;
        }

        var chain = new List<PdfDictionary>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance) { page };
        while (parent is not null && chain.Count < PdfLimits.MaxPageTreeDepth && visited.Add(parent))
        {
            chain.Add(parent);
            parent = parent.GetDictionary(KnownName.Parent);
        }

        // The nearest ancestor is applied last, so it wins.
        var inherited = default(InheritedAttributes);
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            inherited = inherited.With(chain[i]);
        }

        return inherited;
    }

    /// <summary>A page tree node waiting to be visited.</summary>
    /// <param name="Reference">The node, usually a reference.</param>
    /// <param name="Inherited">The attributes it inherits.</param>
    /// <param name="Depth">Its depth in the tree.</param>
    private readonly record struct PendingNode(PdfValue Reference, InheritedAttributes Inherited, int Depth);
}
