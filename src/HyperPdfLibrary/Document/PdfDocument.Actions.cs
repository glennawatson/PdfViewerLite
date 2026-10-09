// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Actions as data: every PDF 2.0 type, <c>/Next</c> chains, additional actions and document scripts. Nothing here runs an action.</content>
public sealed partial class PdfDocument
{
    /// <summary>The most actions read in one chain.</summary>
    private const int MaxActionChain = 1000;

    /// <summary>The subtype of an action synthesised from a catalog <c>/OpenAction</c> destination.</summary>
    private const string GoToSubtype = "GoTo";

    /// <summary>Reads an action with its <c>/Next</c> chain. The chain is cut at a loop and after <see cref="MaxActionChain"/> actions.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action and the actions that follow it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null.</exception>
    public PdfActionNode ReadActionNode(PdfDictionary action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var budget = MaxActionChain;
        return ReadActionNode(action, visited, 0, ref budget);
    }

    /// <summary>Gets the action run when the document opens (the catalog's <c>/OpenAction</c>), which may be a destination.</summary>
    /// <returns>The action, or <see langword="null"/> when there is none.</returns>
    public PdfActionNode? GetOpenAction()
    {
        var open = Catalog.Get(KnownName.OpenAction);
        if (open.AsDictionary() is { } action)
        {
            return ReadActionNode(action);
        }

        return !open.IsNull && ResolveDestination(open) is { } destination
            ? new PdfActionNode(GoToSubtype, new GoToAction(destination), [], new(Objects))
            : null;
    }

    /// <summary>Gets the document's additional actions (the catalog's <c>/AA</c>): will close, will save, did save, will print and did print.</summary>
    /// <returns>The triggers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfTrigger[] GetTriggers() => GetTriggers(Catalog);

    /// <summary>Gets a page's additional actions (<c>/AA</c>): opened (O) and closed (C).</summary>
    /// <param name="page">The page.</param>
    /// <returns>The triggers.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public PdfTrigger[] GetTriggers(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return GetTriggers(page.Dictionary);
    }

    /// <summary>Gets the additional actions of any dictionary: an annotation, a form field or the catalog.</summary>
    /// <param name="owner">The dictionary holding an <c>/AA</c> entry.</param>
    /// <returns>The triggers, in the dictionary's order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public PdfTrigger[] GetTriggers(PdfDictionary owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.GetDictionary(KnownName.AA) is not { } actions)
        {
            return [];
        }

        var triggers = new List<PdfTrigger>(actions.Count);
        for (var i = 0; i < actions.Count; i++)
        {
            if (Objects.Resolve(actions.GetValueAt(i)).AsDictionary() is { } action)
            {
                triggers.Add(new(Objects.Names.GetString(actions.GetKeyAt(i)), ReadActionNode(action)));
            }
        }

        return [.. triggers];
    }

    /// <summary>Gets the document-level scripts of the <c>/Names /JavaScript</c> name tree. The text is data; nothing runs.</summary>
    /// <returns>The scripts in name tree order.</returns>
    public PdfNamedScript[] GetDocumentScripts()
    {
        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(Catalog.GetDictionary(KnownName.Names)?.Dict("JavaScript"), entries);
        var scripts = new List<PdfNamedScript>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Value.AsDictionary()?.Get(KnownName.JS) is { } code && ReadScript(code) is { } script)
            {
                scripts.Add(new(PdfText.Decode(entry.Key.AsStringBytes()), script));
            }
        }

        return [.. scripts];
    }

    /// <summary>Reads an action and the actions after it.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <param name="visited">The actions already read, to stop loops.</param>
    /// <param name="depth">The depth in the chain.</param>
    /// <param name="budget">The actions still allowed.</param>
    /// <returns>The node.</returns>
    private PdfActionNode ReadActionNode(PdfDictionary action, HashSet<PdfDictionary> visited, int depth, ref int budget)
    {
        _ = visited.Add(action);
        budget--;
        var subtype = action.NameText("S") ?? string.Empty;
        return new(subtype, ReadAction(action), ReadNext(action, visited, depth, ref budget), action);
    }

    /// <summary>Reads the <c>/Next</c> entry: one action or an array of them.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <param name="visited">The actions already read.</param>
    /// <param name="depth">The depth of <paramref name="action"/>.</param>
    /// <param name="budget">The actions still allowed.</param>
    /// <returns>The following actions.</returns>
    private PdfActionNode[] ReadNext(PdfDictionary action, HashSet<PdfDictionary> visited, int depth, ref int budget)
    {
        var next = action.Get(KnownName.Next);
        var nodes = new List<PdfActionNode>();
        if (depth + 1 >= PdfLimits.MaxNesting)
        {
            return [];
        }

        if (next.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count && budget > 0; i++)
            {
                AddNext(nodes, array.GetDictionary(i), visited, depth, ref budget);
            }
        }
        else if (budget > 0)
        {
            AddNext(nodes, next.AsDictionary(), visited, depth, ref budget);
        }

        return [.. nodes];
    }

    /// <summary>Adds a next action unless it was read already.</summary>
    /// <param name="nodes">The result list.</param>
    /// <param name="next">The action dictionary, or null.</param>
    /// <param name="visited">The actions already read.</param>
    /// <param name="depth">The depth of the action it follows.</param>
    /// <param name="budget">The actions still allowed.</param>
    private void AddNext(List<PdfActionNode> nodes, PdfDictionary? next, HashSet<PdfDictionary> visited, int depth, ref int budget)
    {
        if (next is not null && !visited.Contains(next))
        {
            nodes.Add(ReadActionNode(next, visited, depth + 1, ref budget));
        }
    }
}
