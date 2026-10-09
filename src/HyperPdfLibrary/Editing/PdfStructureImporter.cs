// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Copies the part of a tagged source document's structure tree that belongs to some pages into a tagged target, for
/// inserted pages. The structure elements that own the pages' marked content and annotations, and their ancestors, are
/// copied under the target's <c>/StructTreeRoot</c>; kids that belong to other pages are left out. Each page gets a new
/// <c>/StructParents</c> key, and new <c>/ParentTree</c> entries list the copied elements by marked content id, so the
/// content streams keep their MCIDs. Role map and class map entries the target lacks are added; the target's win.
/// </summary>
/// <remarks>Not thread-safe. Elements' <c>/ID</c> and <c>/Ref</c> entries are not copied.</remarks>
[DebuggerDisplay("PdfStructureImporter: {_elements.Count} elements")]
internal sealed class PdfStructureImporter
{
    /// <summary>The source elements being copied, by source object number, with their reserved target ids.</summary>
    private readonly Dictionary<int, PdfObjectId> _elements = [];

    /// <summary>The transaction receiving the copies.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The store receiving the copies.</summary>
    private readonly PdfObjectStore _target;

    /// <summary>The store copied from.</summary>
    private readonly PdfObjectStore _source;

    /// <summary>The importer that copies everything but the structure elements.</summary>
    private readonly PdfStoreImporter _importer;

    /// <summary>The target page of each inserted source page, by source object number.</summary>
    private readonly Dictionary<int, PdfObjectId> _pages;

    /// <summary>The source <c>/StructTreeRoot</c>.</summary>
    private readonly PdfDictionary _sourceRoot;

    /// <summary>The target <c>/StructTreeRoot</c> object.</summary>
    private readonly PdfObjectId _targetRootId;

    /// <summary>Initializes a new instance of the <see cref="PdfStructureImporter"/> class.</summary>
    /// <param name="transaction">The transaction receiving the copies.</param>
    /// <param name="target">The store receiving the copies.</param>
    /// <param name="source">The store copied from.</param>
    /// <param name="importer">The importer that copies the other objects.</param>
    /// <param name="pages">The target page of each inserted source page, by source object number.</param>
    /// <param name="roots">The source root and the target root's id.</param>
    private PdfStructureImporter(PdfEditTransaction transaction, PdfObjectStore target, PdfObjectStore source, PdfStoreImporter importer, Dictionary<int, PdfObjectId> pages, StructureRoots roots)
    {
        _transaction = transaction;
        _target = target;
        _source = source;
        _importer = importer;
        _pages = pages;
        _sourceRoot = roots.SourceRoot;
        _targetRootId = roots.TargetRootId;
    }

    /// <summary>Gets the keys copied from a marked content or object reference besides /Type and /Pg.</summary>
    private static ReadOnlySpan<KnownName> ReferenceKeys => [KnownName.MCID, KnownName.Obj];

    /// <summary>Creates an importer when both documents are tagged.</summary>
    /// <param name="transaction">The transaction receiving the copies.</param>
    /// <param name="target">The store receiving the copies.</param>
    /// <param name="source">The store copied from.</param>
    /// <param name="importer">The importer that copies the other objects.</param>
    /// <param name="pages">The target page of each inserted source page, by source object number.</param>
    /// <returns>The importer, or <see langword="null"/> when either document has no structure tree the copy can use.</returns>
    internal static PdfStructureImporter? Create(PdfEditTransaction transaction, PdfObjectStore target, PdfObjectStore source, PdfStoreImporter importer, Dictionary<int, PdfObjectId> pages)
    {
        var sourceRoot = source.Catalog.GetDictionary(KnownName.StructTreeRoot);
        var targetRootId = target.Catalog.GetRaw(KnownName.StructTreeRoot).AsReference();
        return sourceRoot is null || !targetRootId.IsValid || target.GetObject(targetRootId).AsDictionary() is null
            ? null
            : new(transaction, target, source, importer, pages, new(sourceRoot, targetRootId));
    }

    /// <summary>Copies the structure of some pages.</summary>
    /// <param name="sourcePages">The inserted source pages.</param>
    /// <returns>The new <c>/StructParents</c> key of each page, or -1 for a page with no structure.</returns>
    internal int[] Import(ReadOnlySpan<PdfPage> sourcePages)
    {
        var keys = new int[sourcePages.Length];
        Array.Fill(keys, -1);
        var owners = new List<OwnerList>();
        var annotations = new List<AnnotationOwner>();
        var sourceTree = _sourceRoot.GetDictionary(KnownName.ParentTree);
        for (var i = 0; i < sourcePages.Length; i++)
        {
            CollectOwners(sourceTree, sourcePages[i], i, owners, annotations);
        }

        if (owners.Count == 0 && annotations.Count == 0)
        {
            return keys;
        }

        var root = _target.GetObject(_targetRootId).AsDictionary()!.Clone();
        var next = NextKey(root);
        var entries = new List<ParentEntry>(owners.Count + annotations.Count);
        MarkRelevant(owners, annotations);
        foreach (var (pageIndex, list) in owners)
        {
            keys[pageIndex] = next;
            entries.Add(new(next, PdfValue.FromArray(MapOwners(list))));
            next++;
        }

        foreach (var annotation in annotations)
        {
            _importer.SetStructParent(annotation.Number, next);
            entries.Add(new(next, PdfValue.FromReference(_elements[annotation.Owner.Number])));
            next++;
        }

        var tops = WriteElements(root);
        UpdateRoot(root, tops, entries, next);
        return keys;
    }

    /// <summary>Gets the first key not used by the target's parent tree.</summary>
    /// <param name="root">The target's structure tree root.</param>
    /// <returns>The key.</returns>
    private static int NextKey(PdfDictionary root)
    {
        var next = root.GetInt32(KnownName.ParentTreeNextKey, 0);
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateNumbers(root.GetDictionary(KnownName.ParentTree), entries);
        foreach (var entry in entries)
        {
            next = (int)Math.Max(next, entry.Key.AsInteger() + 1);
        }

        return next;
    }

    /// <summary>Reads the elements that own a page's marked content, and those that own its annotations.</summary>
    /// <param name="sourceTree">The source parent tree.</param>
    /// <param name="page">The source page.</param>
    /// <param name="index">The page's position in the inserted pages.</param>
    /// <param name="owners">Receives the page's list of owners by marked content id.</param>
    /// <param name="annotations">Receives the owners of the page's annotations.</param>
    private static void CollectOwners(PdfDictionary? sourceTree, PdfPage page, int index, List<OwnerList> owners, List<AnnotationOwner> annotations)
    {
        var key = page.Dictionary.GetInt32(KnownName.StructParents, -1);
        if (key >= 0 && NameTree.FindNumber(sourceTree, key).AsArray() is { } list)
        {
            owners.Add(new(index, list));
        }

        var annots = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annots is not null && i < annots.Count; i++)
        {
            var number = annots.GetRaw(i).AsReference().Number;
            var annotationKey = annots.GetDictionary(i)?.GetInt32(KnownName.StructParent, -1) ?? -1;
            var owner = annotationKey >= 0 ? NameTree.FindNumberRaw(sourceTree, annotationKey) : default;
            if (owner.IsReference && number != 0)
            {
                annotations.Add(new(number, owner.AsReference()));
            }
        }
    }

    /// <summary>Determines whether an element entry is written by the importer itself or not copied.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> for /P, /K, /Pg, /ID and /Ref.</returns>
    private static bool IsStructural(PdfName key) =>
        key.Is(KnownName.P) || key.Is(KnownName.K) || key.Is(KnownName.Pg) || key.Is(KnownName.ID) || key.Is(KnownName.Ref);

    /// <summary>Determines whether a kid dictionary is a marked content or object reference.</summary>
    /// <param name="kid">The kid.</param>
    /// <returns><see langword="true"/> for /MCR and /OBJR.</returns>
    private static bool IsReference(PdfDictionary kid) => kid.IsName(KnownName.Type, KnownName.MCR) || kid.IsName(KnownName.Type, KnownName.OBJR);

    /// <summary>Finds the source page an element belongs to: its <c>/Pg</c>, else the nearest ancestor's.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The page's source object number, or 0.</returns>
    private static int FindPageNumber(PdfDictionary element)
    {
        var node = element;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxNesting; depth++)
        {
            var page = node.GetRaw(KnownName.Pg).AsReference();
            if (page.IsValid)
            {
                return page.Number;
            }

            node = node.GetDictionary(KnownName.P);
        }

        return 0;
    }

    /// <summary>Reserves a target object for every element that owns copied content and for each of its ancestors.</summary>
    /// <param name="owners">The owners of the pages' marked content.</param>
    /// <param name="annotations">The owners of the pages' annotations.</param>
    private void MarkRelevant(List<OwnerList> owners, List<AnnotationOwner> annotations)
    {
        foreach (var (_, list) in owners)
        {
            for (var i = 0; i < list.Count; i++)
            {
                MarkWithAncestors(list.GetRaw(i).AsReference());
            }
        }

        foreach (var annotation in annotations)
        {
            MarkWithAncestors(annotation.Owner);
        }
    }

    /// <summary>Reserves a target object for an element and its ancestors below the root.</summary>
    /// <param name="start">The element.</param>
    private void MarkWithAncestors(PdfObjectId start)
    {
        var id = start;
        for (var depth = 0; depth < PdfLimits.MaxNesting && id.IsValid && !_elements.ContainsKey(id.Number); depth++)
        {
            if (_source.GetObject(id).AsDictionary() is not { } element || ReferenceEquals(element, _sourceRoot))
            {
                return;
            }

            var reserved = _transaction.Add(default);
            _elements[id.Number] = reserved;
            _importer.MapObject(id, reserved);
            id = element.GetRaw(KnownName.P).AsReference();
        }
    }

    /// <summary>Converts a page's source owner list into target references, keeping each marked content id's position.</summary>
    /// <param name="list">The source list.</param>
    /// <returns>The list in the target; a null where an entry has no copied owner.</returns>
    private PdfArray MapOwners(PdfArray list)
    {
        var mapped = new PdfArray(_target, list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            var id = list.GetRaw(i).AsReference();
            mapped.Add(id.IsValid && _elements.TryGetValue(id.Number, out var target) ? PdfValue.FromReference(target) : default);
        }

        return mapped;
    }

    /// <summary>Writes every reserved element and, when there are several top-level ones, a grouping element for them.</summary>
    /// <param name="root">The target root being rewritten.</param>
    /// <returns>The elements to add to the root's kids.</returns>
    private PdfValue[] WriteElements(PdfDictionary root)
    {
        var tops = FindTops();
        PdfObjectId parent;
        PdfValue[] result;
        if (tops.Count == 1)
        {
            parent = _targetRootId;
            result = [PdfValue.FromReference(_elements[tops[0]])];
        }
        else
        {
            parent = _transaction.Add(default);
            result = [PdfValue.FromReference(parent)];
            WriteGrouping(parent, tops);
        }

        foreach (var (number, id) in _elements)
        {
            var element = _source.GetObject(new(number, 0)).AsDictionary()!;
            var elementParent = element.GetRaw(KnownName.P).AsReference();
            var parentId = _elements.TryGetValue(elementParent.Number, out var mapped) ? mapped : parent;
            _transaction.Replace(id, PdfValue.FromDictionary(CopyElement(element, parentId)));
        }

        MergeMap(root, KnownName.RoleMap);
        MergeMap(root, KnownName.ClassMap);
        return result;
    }

    /// <summary>Writes the grouping element that holds several top-level copies.</summary>
    /// <param name="id">The grouping element's object.</param>
    /// <param name="tops">The source numbers of the top-level elements.</param>
    private void WriteGrouping(PdfObjectId id, List<int> tops)
    {
        var grouping = new PdfDictionary(_target);
        grouping.Set(KnownName.Type, PdfValue.FromName(KnownName.StructElem));
        grouping.Set(KnownName.S, PdfValue.FromName(_target.Names.Intern("Document")));
        grouping.Set(KnownName.P, PdfValue.FromReference(_targetRootId));
        var kids = new PdfArray(_target, tops.Count);
        foreach (var number in tops)
        {
            kids.Add(PdfValue.FromReference(_elements[number]));
        }

        grouping.Set(KnownName.K, PdfValue.FromArray(kids));
        _transaction.Replace(id, PdfValue.FromDictionary(grouping));
    }

    /// <summary>Finds the copied elements that sit directly under the source root, in document order.</summary>
    /// <returns>The source object numbers.</returns>
    private List<int> FindTops()
    {
        var tops = new List<int>();
        var kids = _sourceRoot.GetRaw(KnownName.K);
        if (kids.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                AddTop(tops, array.GetRaw(i));
            }
        }
        else
        {
            AddTop(tops, kids);
        }

        // An element whose parent is not the root (a broken /P) still has to be reachable.
        foreach (var number in _elements.Keys)
        {
            if (!tops.Contains(number) && !IsReachable(number))
            {
                tops.Add(number);
            }
        }

        return tops;
    }

    /// <summary>Adds a root kid when it was copied.</summary>
    /// <param name="tops">The top-level elements.</param>
    /// <param name="kid">The root's kid.</param>
    private void AddTop(List<int> tops, PdfValue kid)
    {
        if (kid.IsReference && _elements.ContainsKey(kid.AsReference().Number))
        {
            tops.Add(kid.AsReference().Number);
        }
    }

    /// <summary>Determines whether a copied element is a kid of another copied element.</summary>
    /// <param name="number">The element's source number.</param>
    /// <returns><see langword="true"/> when its parent was copied or it is a root kid.</returns>
    private bool IsReachable(int number)
    {
        var element = _source.GetObject(new(number, 0)).AsDictionary()!;
        var parent = element.GetRaw(KnownName.P).AsReference();
        return _elements.ContainsKey(parent.Number) || ReferenceEquals(element.GetDictionary(KnownName.P), _sourceRoot);
    }

    /// <summary>Copies a structure element.</summary>
    /// <param name="element">The source element.</param>
    /// <param name="parent">The copy's parent.</param>
    /// <returns>The copy.</returns>
    private PdfDictionary CopyElement(PdfDictionary element, PdfObjectId parent)
    {
        var copy = new PdfDictionary(_target, element.Count);
        for (var i = 0; i < element.Count; i++)
        {
            var key = element.GetKeyAt(i);
            if (IsStructural(key))
            {
                continue;
            }

            var value = _importer.Import(element.GetValueAt(i));
            if (!value.IsNull)
            {
                copy.Add(_importer.ImportName(key), value);
            }
        }

        copy.Set(KnownName.P, PdfValue.FromReference(parent));
        var pageNumber = FindPageNumber(element);
        if (_pages.TryGetValue(pageNumber, out var page))
        {
            copy.Set(KnownName.Pg, PdfValue.FromReference(page));
        }

        var kids = CopyKids(element, pageNumber);
        if (kids.Count > 0)
        {
            copy.Set(KnownName.K, PdfValue.FromArray(kids));
        }

        return copy;
    }

    /// <summary>Copies an element's kids that belong to the inserted pages or to copied elements.</summary>
    /// <param name="element">The source element.</param>
    /// <param name="pageNumber">The element's source page number.</param>
    /// <returns>The kids.</returns>
    private PdfArray CopyKids(PdfDictionary element, int pageNumber)
    {
        var kids = new PdfArray(_target);
        var source = element.GetRaw(KnownName.K);
        if (source.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                AddKid(kids, array.GetRaw(i), pageNumber);
            }
        }
        else
        {
            AddKid(kids, source, pageNumber);
        }

        return kids;
    }

    /// <summary>Copies one kid: a marked content id, a marked content reference, an object reference or an element.</summary>
    /// <param name="kids">The copied kids.</param>
    /// <param name="kid">The source kid.</param>
    /// <param name="pageNumber">The parent element's source page number.</param>
    private void AddKid(PdfArray kids, PdfValue kid, int pageNumber)
    {
        if (kid.IsNumber)
        {
            if (_pages.ContainsKey(pageNumber))
            {
                kids.Add(kid);
            }

            return;
        }

        if (kid.IsReference && _elements.TryGetValue(kid.AsReference().Number, out var element))
        {
            kids.Add(PdfValue.FromReference(element));
            return;
        }

        if (_source.Resolve(kid).AsDictionary() is { } reference)
        {
            AddReferenceKid(kids, reference, pageNumber);
        }
    }

    /// <summary>Copies a marked content or object reference when its page was inserted.</summary>
    /// <param name="kids">The copied kids.</param>
    /// <param name="kid">The source reference dictionary.</param>
    /// <param name="pageNumber">The parent element's source page number.</param>
    private void AddReferenceKid(PdfArray kids, PdfDictionary kid, int pageNumber)
    {
        var own = kid.GetRaw(KnownName.Pg).AsReference();
        if (!_pages.TryGetValue(own.IsValid ? own.Number : pageNumber, out var targetPage) || !IsReference(kid))
        {
            return;
        }

        var copy = new PdfDictionary(_target, kid.Count);
        copy.Set(KnownName.Type, kid.Get(KnownName.Type));
        copy.Set(KnownName.Pg, PdfValue.FromReference(targetPage));
        foreach (var key in ReferenceKeys)
        {
            if (kid.GetRaw(key) is { IsNull: false } value)
            {
                copy.Set(key, _importer.Import(value));
            }
        }

        kids.Add(PdfValue.FromDictionary(copy));
    }

    /// <summary>Adds the source role map or class map entries the target lacks.</summary>
    /// <param name="root">The target root being rewritten.</param>
    /// <param name="key">/RoleMap or /ClassMap.</param>
    private void MergeMap(PdfDictionary root, KnownName key)
    {
        if (_sourceRoot.GetDictionary(key) is not { } sourceMap)
        {
            return;
        }

        var merged = root.GetDictionary(key)?.Clone() ?? new PdfDictionary(_target);
        for (var i = 0; i < sourceMap.Count; i++)
        {
            var name = _importer.ImportName(sourceMap.GetKeyAt(i));
            if (!merged.ContainsKey(name))
            {
                merged.Add(name, _importer.Import(sourceMap.GetValueAt(i)));
            }
        }

        root.Set(key, PdfValue.FromDictionary(merged));
    }

    /// <summary>Adds the copies to the target root, adds the parent tree entries and writes the root.</summary>
    /// <param name="root">The target root.</param>
    /// <param name="tops">The elements to add to the root's kids.</param>
    /// <param name="entries">The new parent tree entries.</param>
    /// <param name="nextKey">The next free parent tree key.</param>
    private void UpdateRoot(PdfDictionary root, PdfValue[] tops, List<ParentEntry> entries, int nextKey)
    {
        var kids = new PdfArray(_target);
        if (root.GetRaw(KnownName.K).AsArray() is { } existing)
        {
            foreach (var kid in existing.Items)
            {
                kids.Add(kid);
            }
        }
        else if (!root.GetRaw(KnownName.K).IsNull)
        {
            kids.Add(root.GetRaw(KnownName.K));
        }

        foreach (var top in tops)
        {
            kids.Add(top);
        }

        root.Set(KnownName.K, PdfValue.FromArray(kids));
        root.Set(KnownName.ParentTree, AppendParentEntries(root.GetRaw(KnownName.ParentTree), entries));
        root.Set(KnownName.ParentTreeNextKey, PdfValue.FromInteger(nextKey));
        _transaction.Replace(_targetRootId, PdfValue.FromDictionary(root));
    }

    /// <summary>Adds entries to the end of a number tree. The new keys are above every key in it.</summary>
    /// <param name="tree">The tree as the root holds it: a reference, a direct node or null.</param>
    /// <param name="entries">The entries, in key order.</param>
    /// <returns>The tree value for the root.</returns>
    private PdfValue AppendParentEntries(PdfValue tree, List<ParentEntry> entries)
    {
        var node = _target.Resolve(tree).AsDictionary()?.Clone() ?? new PdfDictionary(_target);
        if (node.GetArray(KnownName.Kids) is { } kids)
        {
            PdfArray limits = new(_target, [PdfValue.FromInteger(entries[0].Key), PdfValue.FromInteger(entries[^1].Key)]);
            PdfDictionary leaf = new(_target);
            leaf.Set(KnownName.Limits, PdfValue.FromArray(limits));
            leaf.Set(KnownName.Nums, PdfValue.FromArray(AppendPairs(null, entries)));
            var extended = kids.Clone();
            extended.Add(PdfValue.FromReference(_transaction.Add(PdfValue.FromDictionary(leaf))));
            node.Set(KnownName.Kids, PdfValue.FromArray(extended));
        }
        else
        {
            node.Set(KnownName.Nums, PdfValue.FromArray(AppendPairs(node.GetArray(KnownName.Nums), entries)));
        }

        if (!tree.IsReference)
        {
            return PdfValue.FromDictionary(node);
        }

        _transaction.Replace(tree.AsReference(), PdfValue.FromDictionary(node));
        return tree;
    }

    /// <summary>Copies a /Nums array and adds key and value pairs to its end.</summary>
    /// <param name="existing">The current array, or null.</param>
    /// <param name="entries">The entries to add.</param>
    /// <returns>The new array.</returns>
    private PdfArray AppendPairs(PdfArray? existing, List<ParentEntry> entries)
    {
        var numbers = existing?.Clone() ?? new PdfArray(_target);
        foreach (var (key, value) in entries)
        {
            numbers.Add(PdfValue.FromInteger(key));
            numbers.Add(value);
        }

        return numbers;
    }

    /// <summary>The source and target structure roots.</summary>
    /// <param name="SourceRoot">The source <c>/StructTreeRoot</c>.</param>
    /// <param name="TargetRootId">The target <c>/StructTreeRoot</c> object.</param>
    [DebuggerDisplay("StructureRoots: {TargetRootId}")]
    private readonly record struct StructureRoots(PdfDictionary SourceRoot, PdfObjectId TargetRootId);

    /// <summary>The owners of one page's marked content.</summary>
    /// <param name="PageIndex">The page's position in the inserted pages.</param>
    /// <param name="Owners">The source parent tree entry: an owner per marked content id.</param>
    [DebuggerDisplay("OwnerList: page {PageIndex}")]
    private readonly record struct OwnerList(int PageIndex, PdfArray Owners);

    /// <summary>The owner of one annotation.</summary>
    /// <param name="Number">The annotation's source object number.</param>
    /// <param name="Owner">The element that owns it.</param>
    [DebuggerDisplay("AnnotationOwner: {Number}")]
    private readonly record struct AnnotationOwner(int Number, PdfObjectId Owner);

    /// <summary>A new parent tree entry.</summary>
    /// <param name="Key">The key.</param>
    /// <param name="Value">The value: an array of owners or one owner.</param>
    [DebuggerDisplay("ParentEntry: {Key}")]
    private readonly record struct ParentEntry(int Key, PdfValue Value);
}
