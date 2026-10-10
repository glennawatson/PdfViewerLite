// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads web capture information.</summary>
public static class PdfDocumentWebCapture
{
    /// <summary>The most content sets read.</summary>
    private const int MaxContentSets = 10_000;

    /// <summary>Gets the web capture information (<c>/SpiderInfo</c> and the <c>/IDS</c> and <c>/URLS</c> name trees).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The information, or <see langword="null"/> when the document has none.</returns>
    public static PdfWebCapture? GetWebCapture(PdfDocument document)
    {
        var spider = document.Catalog.Dict("SpiderInfo");
        var names = document.Catalog.GetDictionary(KnownName.Names);
        var ids = new List<NameTreeEntry>();
        var urls = new List<NameTreeEntry>();
        NameTree.Enumerate(names?.Dict("IDS"), ids);
        NameTree.Enumerate(names?.Dict("URLS"), urls);
        if (spider is null && ids.Count == 0 && urls.Count == 0)
        {
            return null;
        }

        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var sets = new List<PdfWebCaptureContentSet>();
        PdfDocumentWebCapture.AddContentSets(document, sets, seen, ids);
        PdfDocumentWebCapture.AddContentSets(document, sets, seen, urls);
        return new(spider?.Num("V", 0) ?? 0, [.. sets], ids.Count, urls.Count);
    }

    /// <summary>Gets the web capture content set identifier of a page (its <c>/ID</c>).</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The identifier, or <see langword="null"/> when the page has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static byte[]? GetWebCaptureId(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var id = page.Dictionary.Value("ID");
        return id.Kind == PdfKind.String ? id.AsStringBytes().ToArray() : null;
    }

    /// <summary>Gets the document part hierarchy (the catalog's <c>/DPartRoot</c>).</summary>
    /// <param name="document">The document.</param>
    /// <returns>The root, or <see langword="null"/> when the document has none.</returns>
    public static PdfDocumentPartRoot? GetDocumentParts(PdfDocument document)
    {
        if (document.Catalog.Dict("DPartRoot") is not { } root)
        {
            return null;
        }

        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var node = root.Dict("DPartRootNode") is { } top ? PdfDocumentWebCapture.ReadPartNode(document, top, 0, visited) : null;
        return new(root.Int("RecordLevel", 0), root.Array("NodeNameList").NameTexts(), node);
    }

    /// <summary>Gets the document part node a page belongs to (its <c>/DPart</c>).</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The node, or <see langword="null"/> when the page names none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PdfDocumentPartNode? GetDocumentPart(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.Dictionary.Dict("DPart") is { } node ? PdfDocumentWebCapture.ReadPartNode(document, node, 0, [with(ReferenceEqualityComparer.Instance)]) : null;
    }

    /// <summary>Adds the content sets of a name tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="sets">The result list.</param>
    /// <param name="seen">The content set dictionaries already added.</param>
    /// <param name="entries">The name tree entries.</param>
    private static void AddContentSets(PdfDocument document, List<PdfWebCaptureContentSet> sets, HashSet<PdfDictionary> seen, List<NameTreeEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (sets.Count < PdfDocumentWebCapture.MaxContentSets && entry.Value.AsDictionary() is { } set && seen.Add(set))
            {
                sets.Add(new(
        set.NameText("S") ?? string.Empty,
        set.GetStringBytes(set.Key("ID")).ToArray(),
        set.Text("CT"),
        PdfDocumentWebCapture.ReadSources(
        document,
        set.Value("SI")),
        set.Array("O")?.Count ?? 0));
            }
        }
    }

    /// <summary>Reads source information: one dictionary or an array of them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The <c>/SI</c> value.</param>
    /// <returns>The sources.</returns>
    private static PdfWebCaptureSource[] ReadSources(PdfDocument document, PdfValue info)
    {
        var sources = new List<PdfWebCaptureSource>();
        if (info.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                PdfDocumentWebCapture.AddSource(document, sources, array.GetDictionary(i));
            }
        }
        else
        {
            PdfDocumentWebCapture.AddSource(document, sources, info.AsDictionary());
        }

        return [.. sources];
    }

    /// <summary>Adds one source information dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="sources">The result list.</param>
    /// <param name="source">The dictionary, or null.</param>
    private static void AddSource(PdfDocument document, List<PdfWebCaptureSource> sources, PdfDictionary? source)
    {
        if (source is not null)
        {
            sources.Add(new(
        PdfDocumentFileSpecs.ReadFileSpec(
        document,
        source.Value("AU")),
        PdfDate.Parse(source.Value("TS").AsStringBytes()),
        PdfDate.Parse(source.Value("E").AsStringBytes()),
        source.Int(
        "S",
        0)));
        }
    }

    /// <summary>Reads a document part node and the nodes below it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="node">The node dictionary.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="visited">The nodes already read.</param>
    /// <returns>The node.</returns>
    private static PdfDocumentPartNode ReadPartNode(PdfDocument document, PdfDictionary node, int depth, HashSet<PdfDictionary> visited)
    {
        _ = visited.Add(node);
        var children = new List<PdfDocumentPartNode>();
        var groups = node.Array("DParts");
        for (var i = 0; groups is not null && depth < PdfLimits.MaxNesting && i < groups.Count; i++)
        {
            PdfDocumentWebCapture.AddPartChildren(document, children, groups.Get(i), depth, visited);
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var dpm = node.Dict("DPM");
        for (var i = 0; dpm is not null && i < dpm.Count; i++)
        {
            if (StoreReading.Resolve(document.Objects, dpm.GetValueAt(i)).ScalarText(document.Objects) is { } text)
            {
                metadata[document.Objects.Names.GetString(dpm.GetKeyAt(i))] = text;
            }
        }

        return new(metadata, [.. children], PdfDocumentWebCapture.PartPage(document, node, "Start"), PdfDocumentWebCapture.PartPage(document, node, "End"), node);
    }

    /// <summary>Adds the children held in one element of <c>/DParts</c>: a node or an array of nodes.</summary>
    /// <param name="document">The document.</param>
    /// <param name="children">The result list.</param>
    /// <param name="value">The element.</param>
    /// <param name="depth">The parent's depth.</param>
    /// <param name="visited">The nodes already read.</param>
    private static void AddPartChildren(PdfDocument document, List<PdfDocumentPartNode> children, PdfValue value, int depth, HashSet<PdfDictionary> visited)
    {
        if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                PdfDocumentWebCapture.AddPartChild(document, children, array.GetDictionary(i), depth, visited);
            }
        }
        else
        {
            PdfDocumentWebCapture.AddPartChild(document, children, value.AsDictionary(), depth, visited);
        }
    }

    /// <summary>Adds one child node unless it was read already.</summary>
    /// <param name="document">The document.</param>
    /// <param name="children">The result list.</param>
    /// <param name="child">The node, or null.</param>
    /// <param name="depth">The parent's depth.</param>
    /// <param name="visited">The nodes already read.</param>
    private static void AddPartChild(PdfDocument document, List<PdfDocumentPartNode> children, PdfDictionary? child, int depth, HashSet<PdfDictionary> visited)
    {
        if (child is not null && !visited.Contains(child))
        {
            children.Add(PdfDocumentWebCapture.ReadPartNode(document, child, depth + 1, visited));
        }
    }

    /// <summary>Gets the page index a leaf node's <c>/Start</c> or <c>/End</c> names.</summary>
    /// <param name="document">The document.</param>
    /// <param name="node">The node.</param>
    /// <param name="key">The key.</param>
    /// <returns>The page index, or null.</returns>
    private static int? PartPage(PdfDocument document, PdfDictionary node, string key)
    {
        var page = node.RawValue(key);
        return page.IsReference && PdfDocumentPages.GetPageIndex(document, page.AsReference()) is var index and >= 0 ? index : null;
    }
}
