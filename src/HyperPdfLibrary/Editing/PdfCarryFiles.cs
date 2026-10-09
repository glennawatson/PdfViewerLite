// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Keeps the names of embedded files that copied pages use. A file specification that a copied page reaches (through
/// <c>/AF</c>, a file attachment annotation or a rich media asset) is copied with the page. When the source also listed it
/// in the <c>/Names /EmbeddedFiles</c> name tree, the copy is listed there too in the target, renamed when the target
/// already has that name.
/// </summary>
[DebuggerDisplay("PdfCarryFiles")]
internal sealed class PdfCarryFiles
{
    /// <summary>The context.</summary>
    private readonly PdfCarryContext _context;

    /// <summary>Initializes a new instance of the <see cref="PdfCarryFiles"/> class.</summary>
    /// <param name="context">The context.</param>
    internal PdfCarryFiles(PdfCarryContext context) => _context = context;

    /// <summary>Lists the copied file specifications in the target's embedded file name tree.</summary>
    internal void Finish()
    {
        var sourceRoot = _context.Source.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles);
        if (sourceRoot is null)
        {
            return;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateRaw(sourceRoot, entries);
        var added = new List<NameTreeEntry>();
        HashSet<string>? used = null;
        foreach (var entry in entries)
        {
            var mapped = _context.Sink.GetMapped(entry.Value.AsReference().Number);
            if (mapped <= 0)
            {
                continue;
            }

            used ??= PdfNameTreeMerge.GetNames(_context.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles));
            var name = PdfCarryText.Unique(entry.Key.AsStringBytes().ToArray(), used);
            added.Add(new(PdfValue.FromString(name), PdfValue.FromReference(new(mapped, 0))));
        }

        if (added.Count == 0)
        {
            return;
        }

        var names = _context.Names;
        _context.SetEntry(names, KnownName.EmbeddedFiles, PdfNameTreeMerge.Merge(_context.TargetStore, names.GetDictionary(KnownName.EmbeddedFiles), added));
    }
}
