// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <content>The structure elements, the parent tree and the catalog entries.</content>
internal sealed partial class InferredTagger
{
    /// <summary>The entries of a structure element.</summary>
    private const int ElementEntries = 5;

    /// <summary>Creates a page's section and its elements, and the page's parent tree entry.</summary>
    /// <param name="page">The page.</param>
    /// <param name="blocks">The elements, in reading order.</param>
    /// <param name="labels">Each unit's label.</param>
    /// <param name="key">The page's parent tree key.</param>
    private void AddSection(PdfPage page, List<TagBlock> blocks, TagLabel[] labels, int key)
    {
        var store = _document.Objects;
        var pageRef = PdfValue.FromReference(page.Id);
        var section = store.Add(PdfValue.Null);
        var kids = new PdfArray(store, blocks.Count);
        var byMcid = new PdfValue[labels.Length];
        var used = 0;
        foreach (var block in blocks)
        {
            var element = store.Add(PdfValue.Null);
            var ids = new PdfArray(store, block.Units.Count);
            foreach (var unit in block.Units)
            {
                ids.Add(PdfValue.FromInteger(labels[unit].Mcid));
                byMcid[labels[unit].Mcid] = PdfValue.FromReference(element);
                used = Math.Max(used, labels[unit].Mcid + 1);
            }

            store.Replace(element, PdfValue.FromDictionary(Element(block.Tag, PdfValue.FromReference(section), pageRef, PdfValue.FromArray(ids))));
            kids.Add(PdfValue.FromReference(element));
            _figures += block.IsFigure ? 1 : 0;
        }

        store.Replace(section, PdfValue.FromDictionary(Element(_names.Sect, PdfValue.FromReference(_documentElement), pageRef, PdfValue.FromArray(kids))));
        _sections.Add(PdfValue.FromReference(section));
        _nextKey++;
        _parentTree.Add(PdfValue.FromInteger(key));
        _parentTree.Add(PdfValue.FromReference(store.Add(PdfValue.FromArray(new(store, byMcid.AsSpan(0, used))))));
    }

    /// <summary>Creates the document element and the structure tree root, and points the catalog at them.</summary>
    private void FinishTree()
    {
        var store = _document.Objects;
        var root = store.Add(PdfValue.Null);
        var sections = new PdfArray(store, _sections.Count);
        foreach (var section in _sections)
        {
            sections.Add(section);
        }

        var document = Element(_names.Document, PdfValue.FromReference(root), PdfValue.Null, PdfValue.FromArray(sections));
        store.Replace(_documentElement, PdfValue.FromDictionary(document));

        var parentTree = new PdfDictionary(store, 1);
        parentTree.Set(KnownName.Nums, PdfValue.FromArray(new(store, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_parentTree))));
        var tree = new PdfDictionary(store, ElementEntries);
        tree.Set(KnownName.Type, PdfValue.FromName(KnownName.StructTreeRoot));
        tree.Set(KnownName.K, PdfValue.FromReference(_documentElement));
        tree.Set(KnownName.ParentTree, PdfValue.FromDictionary(parentTree));
        tree.Set(_names.ParentTreeNextKey, PdfValue.FromInteger(_nextKey));
        store.Replace(root, PdfValue.FromDictionary(tree));

        var catalogRef = store.Trailer.GetRaw(KnownName.Root);
        var catalog = store.Resolve(catalogRef).AsDictionary()!.Clone();
        catalog.Set(KnownName.StructTreeRoot, PdfValue.FromReference(root));
        var markInfo = catalog.GetDictionary(KnownName.MarkInfo)?.Clone() ?? new PdfDictionary(store, 1);
        markInfo.Set(_names.Marked, PdfValue.FromBoolean(true));
        catalog.Set(KnownName.MarkInfo, PdfValue.FromDictionary(markInfo));
        store.Replace(catalogRef.AsReference(), PdfValue.FromDictionary(catalog));
        _document.RefreshAfterOptimizerEdit();
    }

    /// <summary>Builds a structure element dictionary.</summary>
    /// <param name="type">The structure type.</param>
    /// <param name="parent">The parent element or root.</param>
    /// <param name="page">The page, or null for the document element.</param>
    /// <param name="kids">The children: marked content ids or elements.</param>
    /// <returns>The dictionary.</returns>
    private PdfDictionary Element(PdfName type, PdfValue parent, PdfValue page, PdfValue kids)
    {
        var element = new PdfDictionary(_document.Objects, ElementEntries);
        element.Set(KnownName.Type, PdfValue.FromName(KnownName.StructElem));
        element.Set(KnownName.S, PdfValue.FromName(type));
        element.Set(KnownName.P, parent);
        if (!page.IsNull)
        {
            element.Set(KnownName.Pg, page);
        }

        element.Set(KnownName.K, kids);
        return element;
    }
}
