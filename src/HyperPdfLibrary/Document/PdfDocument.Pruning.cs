// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>
/// Drops outline entries and named destinations that lead to deleted pages. An outline entry with children keeps its
/// place but loses its destination; a leaf entry is unlinked and the open counts of its ancestors are corrected.
/// </content>
public sealed partial class PdfDocument
{
    /// <summary>Gets the entries of a name tree leaf per pair.</summary>
    private const int NamePairLength = 2;

    /// <summary>Points a sibling's /Prev or /Next at another entry, or removes the link.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="id">The sibling, or an invalid id for none.</param>
    /// <param name="key">/Prev or /Next.</param>
    /// <param name="value">The new target, or null.</param>
    private static void SetLink(PdfEditTransaction transaction, PdfObjectId id, KnownName key, PdfValue value)
    {
        if (!id.IsValid || transaction.CloneDictionary(id) is not { } sibling)
        {
            return;
        }

        SetOrRemove(sibling, key, value);
        transaction.Replace(id, PdfValue.FromDictionary(sibling));
    }

    /// <summary>Sets a key, or removes it for a null value.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    private static void SetOrRemove(PdfDictionary dictionary, PdfName key, PdfValue value)
    {
        if (value.IsNull)
        {
            _ = dictionary.Remove(key);
            return;
        }

        dictionary.Set(key, value);
    }

    /// <summary>
    /// Lowers the visible-descendant counts after one entry is removed: each open ancestor loses one and passes the change
    /// up; the first closed ancestor loses one from its hidden count and stops it.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="id">The removed entry's parent.</param>
    private static void ReduceOpenCounts(PdfEditTransaction transaction, PdfObjectId id)
    {
        for (var depth = 0; id.IsValid && depth < MaxOutlineDepth; depth++)
        {
            var node = transaction.CloneDictionary(id);
            var count = node?.GetInt32(KnownName.Count) ?? 0;
            if (node is null || count == 0)
            {
                return;
            }

            var reduced = count > 0 ? count - 1 : count + 1;
            SetOrRemove(node, KnownName.Count, reduced == 0 ? default : PdfValue.FromInteger(reduced));
            transaction.Replace(id, PdfValue.FromDictionary(node));
            if (count < 0)
            {
                return;
            }

            id = node.GetRaw(KnownName.Parent).AsReference();
        }
    }

    /// <summary>Removes the outline entries and named destinations that lead to deleted pages.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    private void PruneDestinations(PdfEditTransaction transaction, HashSet<int> deleted)
    {
        PruneOutline(transaction, deleted);
        PruneDestsDictionary(transaction, deleted);
        PruneDestsTree(transaction, deleted);
    }

    /// <summary>Determines whether a destination leads to a deleted page.</summary>
    /// <param name="destination">The destination: an array, a name, a string or a dictionary holding /D.</param>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    /// <param name="followNames">Whether to look names up in the named destinations.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private bool LeadsToDeleted(PdfValue destination, HashSet<int> deleted, bool followNames)
    {
        var value = Objects.Resolve(destination);
        if (followNames && value.Kind is PdfKind.Name or PdfKind.String)
        {
            value = FindNamedDestination(value);
        }

        if (value.AsDictionary() is { } wrapped)
        {
            value = wrapped.Get(KnownName.D);
        }

        var page = value.AsArray()?.GetRaw(0) ?? default;
        return page.IsReference && deleted.Contains(page.AsReference().Number);
    }

    /// <summary>Determines whether an outline entry leads to a deleted page, by /Dest or a go-to action.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private bool EntryLeadsToDeleted(PdfDictionary entry, HashSet<int> deleted)
    {
        if (LeadsToDeleted(entry.GetRaw(KnownName.Dest), deleted, true))
        {
            return true;
        }

        return entry.GetDictionary(KnownName.A) is { } action && action.IsName(KnownName.S, KnownName.GoTo)
            && LeadsToDeleted(action.GetRaw(KnownName.D), deleted, true);
    }

    /// <summary>Walks the outline and sorts the entries that lead to deleted pages.</summary>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    /// <param name="strip">Receives entries with children, which keep their place but lose their destination.</param>
    /// <param name="unlink">Receives leaf entries, which are removed.</param>
    private void FindOutlineEntries(HashSet<int> deleted, List<PdfObjectId> strip, List<PdfObjectId> unlink)
    {
        var visited = new HashSet<int>();
        var pending = new Stack<OutlineVisit>();
        pending.Push(new(Catalog.GetDictionary(KnownName.Outlines)?.GetRaw(KnownName.First) ?? default, 0));
        var budget = MaxOutlineEntries;
        while (budget > 0 && pending.TryPop(out var visit))
        {
            if (ReadOutlineEntry(visit, visited) is not { } entry)
            {
                continue;
            }

            budget--;
            var first = entry.GetRaw(KnownName.First);
            if (EntryLeadsToDeleted(entry, deleted))
            {
                (first.IsReference ? strip : unlink).Add(visit.Entry.AsReference());
            }

            pending.Push(new(entry.GetRaw(KnownName.Next), visit.Depth));
            pending.Push(new(first, visit.Depth + 1));
        }
    }

    /// <summary>Reads an outline entry reached by reference, once, within the depth limit.</summary>
    /// <param name="visit">The entry as reached.</param>
    /// <param name="visited">The entries already read.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    private PdfDictionary? ReadOutlineEntry(in OutlineVisit visit, HashSet<int> visited)
    {
        var id = visit.Entry.AsReference();
        return id.IsValid && visit.Depth < MaxOutlineDepth && visited.Add(id.Number) ? Objects.GetDictionary(id) : null;
    }

    /// <summary>Finds and fixes outline entries that lead to deleted pages.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    private void PruneOutline(PdfEditTransaction transaction, HashSet<int> deleted)
    {
        var strip = new List<PdfObjectId>();
        var unlink = new List<PdfObjectId>();
        FindOutlineEntries(deleted, strip, unlink);
        foreach (var id in strip)
        {
            var copy = transaction.CloneDictionary(id)!;
            _ = copy.Remove(KnownName.Dest);
            _ = copy.Remove(KnownName.A);
            transaction.Replace(id, PdfValue.FromDictionary(copy));
        }

        foreach (var id in unlink)
        {
            UnlinkOutlineEntry(transaction, id);
        }
    }

    /// <summary>Unlinks a leaf outline entry from its siblings and parent, then deletes it.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="id">The entry.</param>
    private void UnlinkOutlineEntry(PdfEditTransaction transaction, PdfObjectId id)
    {
        if (Objects.GetDictionary(id) is not { } entry)
        {
            return;
        }

        var previous = entry.GetRaw(KnownName.Prev);
        var next = entry.GetRaw(KnownName.Next);
        SetLink(transaction, previous.AsReference(), KnownName.Next, next);
        SetLink(transaction, next.AsReference(), KnownName.Prev, previous);
        var parentId = entry.GetRaw(KnownName.Parent).AsReference();
        if (transaction.CloneDictionary(parentId) is { } parent)
        {
            if (parent.GetRaw(KnownName.First).AsReference().Number == id.Number)
            {
                SetOrRemove(parent, KnownName.First, next);
            }

            if (parent.GetRaw(KnownName.Last).AsReference().Number == id.Number)
            {
                SetOrRemove(parent, KnownName.Last, previous);
            }

            transaction.Replace(parentId, PdfValue.FromDictionary(parent));
            ReduceOpenCounts(transaction, parentId);
        }

        transaction.Delete(id);
    }

    /// <summary>Removes entries of the catalog's /Dests dictionary that lead to deleted pages.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    private void PruneDestsDictionary(PdfEditTransaction transaction, HashSet<int> deleted)
    {
        var raw = Catalog.GetRaw(KnownName.Dests);
        if (Objects.Resolve(raw).AsDictionary() is not { } dests)
        {
            return;
        }

        var copy = new PdfDictionary(Objects, dests.Count);
        for (var i = 0; i < dests.Count; i++)
        {
            if (!LeadsToDeleted(dests.GetValueAt(i), deleted, false))
            {
                copy.Add(dests.GetKeyAt(i), dests.GetValueAt(i));
            }
        }

        if (copy.Count == dests.Count)
        {
            return;
        }

        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(copy));
            return;
        }

        var catalog = Catalog.Clone();
        catalog.Set(KnownName.Dests, PdfValue.FromDictionary(copy));
        PdfPageTreeWriter.ReplaceCatalog(transaction, Objects, catalog);
    }

    /// <summary>Removes entries of the /Names /Dests name tree that lead to deleted pages.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    private void PruneDestsTree(PdfEditTransaction transaction, HashSet<int> deleted)
    {
        var namesRaw = Catalog.GetRaw(KnownName.Names);
        var rootRaw = Objects.Resolve(namesRaw).AsDictionary()?.GetRaw(KnownName.Dests) ?? default;
        var visited = new HashSet<int>();
        var pending = new Stack<OutlineVisit>();
        pending.Push(new(rootRaw, 0));
        while (pending.TryPop(out var visit))
        {
            if (ReadTreeNode(visit, visited) is not { } node)
            {
                continue;
            }

            if (FilterLeaf(node, deleted) is { } filtered)
            {
                WriteTreeNode(transaction, visit, filtered, namesRaw);
            }

            var kids = node.GetArray(KnownName.Kids);
            for (var i = 0; kids is not null && i < kids.Count; i++)
            {
                pending.Push(new(kids.GetRaw(i), visit.Depth + 1));
            }
        }
    }

    /// <summary>Reads a name tree node once, within the depth limit.</summary>
    /// <param name="visit">The node as reached.</param>
    /// <param name="visited">The indirect nodes already read.</param>
    /// <returns>The node, or <see langword="null"/>.</returns>
    private PdfDictionary? ReadTreeNode(in OutlineVisit visit, HashSet<int> visited) =>
        visit.Depth >= PdfLimits.MaxPageTreeDepth || (visit.Entry.IsReference && !visited.Add(visit.Entry.AsReference().Number))
            ? null
            : Objects.Resolve(visit.Entry).AsDictionary();

    /// <summary>Copies a name tree node without the pairs that lead to deleted pages.</summary>
    /// <param name="node">The node.</param>
    /// <param name="deleted">The object numbers of the deleted pages.</param>
    /// <returns>The filtered copy, or <see langword="null"/> when nothing was removed.</returns>
    private PdfDictionary? FilterLeaf(PdfDictionary node, HashSet<int> deleted)
    {
        if (node.GetArray(KnownName.Names) is not { } pairs)
        {
            return null;
        }

        var kept = new PdfArray(Objects, pairs.Count);
        for (var i = 0; i + 1 < pairs.Count; i += NamePairLength)
        {
            if (LeadsToDeleted(pairs.GetRaw(i + 1), deleted, false))
            {
                continue;
            }

            kept.Add(pairs.GetRaw(i));
            kept.Add(pairs.GetRaw(i + 1));
        }

        if (kept.Count == pairs.Count - (pairs.Count % NamePairLength))
        {
            return null;
        }

        var copy = node.Clone();
        copy.Set(KnownName.Names, PdfValue.FromArray(kept));
        return copy;
    }

    /// <summary>Stores a filtered name tree node: in place when indirect, or through /Names when it is the direct root.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="visit">The node as reached.</param>
    /// <param name="filtered">The filtered node.</param>
    /// <param name="namesRaw">The catalog's /Names entry.</param>
    private void WriteTreeNode(PdfEditTransaction transaction, in OutlineVisit visit, PdfDictionary filtered, PdfValue namesRaw)
    {
        if (visit.Entry.IsReference)
        {
            transaction.Replace(visit.Entry.AsReference(), PdfValue.FromDictionary(filtered));
            return;
        }

        // Only the root can be direct and reachable here without its holder; deeper direct kids are left as they are.
        if (visit.Depth != 0 || Objects.Resolve(namesRaw).AsDictionary() is not { } names)
        {
            return;
        }

        var namesCopy = names.Clone();
        namesCopy.Set(KnownName.Dests, PdfValue.FromDictionary(filtered));
        if (namesRaw.IsReference)
        {
            transaction.Replace(namesRaw.AsReference(), PdfValue.FromDictionary(namesCopy));
            return;
        }

        var catalog = Catalog.Clone();
        catalog.Set(KnownName.Names, PdfValue.FromDictionary(namesCopy));
        PdfPageTreeWriter.ReplaceCatalog(transaction, Objects, catalog);
    }

    /// <summary>An outline entry or name tree node waiting to be visited.</summary>
    /// <param name="Entry">The entry as stored, usually a reference.</param>
    /// <param name="Depth">Its depth.</param>
    private readonly record struct OutlineVisit(PdfValue Entry, int Depth);
}
