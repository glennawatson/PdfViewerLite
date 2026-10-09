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
        var ancestors = new Stack<AncestorCursor>();
        var node = new PendingNode(root, default, 0);
        while (true)
        {
            PdfOpenContext.ThrowIfCancelled(objects.Context);
            if (Visit(objects, node, ancestors, visited, pages, out var firstChild))
            {
                node = firstChild;
                continue;
            }

            if (!TryNextChild(ancestors, out node))
            {
                break;
            }
        }

        return pages.Count > 0 ? [.. pages] : ScanForPages(objects);
    }

    /// <summary>Visits one node: adds a page, or enters its first kid.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="node">The node.</param>
    /// <param name="ancestors">The active ancestors and their next kid indexes.</param>
    /// <param name="visited">The object numbers already visited.</param>
    /// <param name="pages">The pages found.</param>
    /// <param name="firstChild">The first kid to visit, when the node has kids.</param>
    /// <returns>Whether the node has a first kid.</returns>
    private static bool Visit(PdfObjectStore objects, in PendingNode node, Stack<AncestorCursor> ancestors, HashSet<int> visited, List<PdfPage> pages, out PendingNode firstChild)
    {
        firstChild = default;
        var id = node.Reference.AsReference();
        var dictionary = objects.Resolve(node.Reference).AsDictionary();
        if (dictionary is null)
        {
            return false;
        }

        var kids = dictionary.GetArray(KnownName.Kids);
        if (kids is null || dictionary.IsName(KnownName.Type, KnownName.Page))
        {
            pages.Add(new(pages.Count, id, dictionary, node.Inherited));
            return false;
        }

        // A page listed twice is listed twice, as in PDFium; only intermediate nodes are guarded against loops.
        if (node.Depth >= PdfLimits.MaxPageTreeDepth)
        {
            PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.RecursionLimit, "The page tree was too deep and was cut off.", id.Number, -1);
            return false;
        }

        if (id.IsValid && !visited.Add(id.Number))
        {
            PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.RecursionLimit, "The page tree loops back on itself; the repeat was ignored.", id.Number, -1);
            return false;
        }

        var inherited = node.Inherited.With(dictionary);
        if (kids.Count == 0)
        {
            return false;
        }

        var depth = node.Depth + 1;
        if (kids.Count > 1)
        {
            ancestors.Push(new(kids, 1, kids.Count, inherited, depth));
        }

        firstChild = new(kids.GetRaw(0), inherited, depth);
        return true;
    }

    /// <summary>Advances to the next kid of the nearest ancestor that has one.</summary>
    /// <param name="ancestors">The active ancestors.</param>
    /// <param name="child">The next kid, when present.</param>
    /// <returns>Whether another kid remains.</returns>
    private static bool TryNextChild(Stack<AncestorCursor> ancestors, out PendingNode child)
    {
        while (ancestors.TryPop(out var ancestor))
        {
            if (ancestor.NextChildIndex >= ancestor.ChildCount)
            {
                continue;
            }

            child = new(ancestor.Kids.GetRaw(ancestor.NextChildIndex), ancestor.Inherited, ancestor.Depth);
            if (ancestor.NextChildIndex + 1 < ancestor.ChildCount)
            {
                ancestors.Push(ancestor with { NextChildIndex = ancestor.NextChildIndex + 1 });
            }

            return true;
        }

        child = default;
        return false;
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

    /// <summary>An ancestor whose remaining kids are visited in document order.</summary>
    /// <param name="Kids">The ancestor's kids.</param>
    /// <param name="NextChildIndex">The next kid's index.</param>
    /// <param name="ChildCount">The number of kids when the ancestor was entered.</param>
    /// <param name="Inherited">The attributes its kids inherit.</param>
    /// <param name="Depth">The kids' depth.</param>
    private readonly record struct AncestorCursor(PdfArray Kids, int NextChildIndex, int ChildCount, InheritedAttributes Inherited, int Depth);
}
