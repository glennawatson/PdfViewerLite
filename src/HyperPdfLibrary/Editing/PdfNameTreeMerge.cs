// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Adds entries to a name tree by writing one sorted leaf node that holds the old entries and the new ones.</summary>
internal static class PdfNameTreeMerge
{
    /// <summary>The values per entry in a /Names array: the key and the value.</summary>
    private const int PairLength = 2;

    /// <summary>Gets the names a tree already holds.</summary>
    /// <param name="root">The tree's root, or <see langword="null"/>.</param>
    /// <returns>The names, by <see cref="PdfCarryText.Key"/>.</returns>
    internal static HashSet<string> GetNames(PdfDictionary? root)
    {
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateRaw(root, entries);
        var names = new HashSet<string>(entries.Count, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            _ = names.Add(PdfCarryText.Key(entry.Key.AsStringBytes()));
        }

        return names;
    }

    /// <summary>Builds a tree of the old entries and the new ones.</summary>
    /// <param name="owner">The store the new nodes belong to, or <see langword="null"/>.</param>
    /// <param name="existing">The tree's current root, or <see langword="null"/>.</param>
    /// <param name="added">The new entries; their keys are strings and their values are as they are stored.</param>
    /// <returns>The new root, a single leaf with its keys in order.</returns>
    internal static PdfDictionary Merge(PdfObjectStore? owner, PdfDictionary? existing, List<NameTreeEntry> added)
    {
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateRaw(existing, entries);
        entries.AddRange(added);
        _ = entries.RemoveAll(static entry => entry.Key.Kind != PdfKind.String);
        entries.Sort(static (a, b) => a.Key.AsStringBytes().SequenceCompareTo(b.Key.AsStringBytes()));
        var names = new PdfArray(owner, entries.Count * PairLength);
        foreach (var entry in entries)
        {
            names.Add(entry.Key);
            names.Add(entry.Value);
        }

        var root = new PdfDictionary(owner);
        root.Set(KnownName.Names, PdfValue.FromArray(names));
        return root;
    }
}
