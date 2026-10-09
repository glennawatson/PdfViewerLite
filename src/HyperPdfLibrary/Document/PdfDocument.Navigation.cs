// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Destinations, actions and the outline.</content>
public sealed partial class PdfDocument
{
    /// <summary>The most outline entries read, guarding against huge or looping outlines.</summary>
    private const int MaxOutlineEntries = 20_000;

    /// <summary>The deepest outline nesting read.</summary>
    private const int MaxOutlineDepth = 64;

    /// <summary>The position of the left coordinate in an /XYZ, /FitV or /FitR destination.</summary>
    private const int LeftSlot = 2;

    /// <summary>The position of the top coordinate in an /XYZ destination.</summary>
    private const int XyzTopSlot = 3;

    /// <summary>The position of the zoom in an /XYZ destination.</summary>
    private const int XyzZoomSlot = 4;

    /// <summary>The position of the top coordinate in a /FitR destination.</summary>
    private const int FitRTopSlot = 5;

    /// <summary>The outline, read on first use.</summary>
    private PdfOutlineItem[]? _outline;

    /// <summary>Gets the outline (bookmarks).</summary>
    /// <returns>The root entries.</returns>
    public IReadOnlyList<PdfOutlineItem> GetOutline()
    {
        var outline = Volatile.Read(ref _outline);
        if (outline is not null)
        {
            return outline;
        }

        var budget = MaxOutlineEntries;
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var first = Catalog.GetDictionary(KnownName.Outlines)?.GetDictionary(KnownName.First);
        outline = [.. ReadOutlineLevel(first, 0, visited, ref budget)];
        Volatile.Write(ref _outline, outline);
        return outline;
    }

    /// <summary>Resolves a destination: an explicit array, or a name looked up in the document's named destinations.</summary>
    /// <param name="value">The destination value.</param>
    /// <returns>The destination, or <see langword="null"/> when it leads nowhere.</returns>
    public PdfDestination? ResolveDestination(PdfValue value)
    {
        value = Objects.Resolve(value);
        if (value.Kind is PdfKind.Name or PdfKind.String)
        {
            value = FindNamedDestination(value);
        }

        if (value.AsDictionary() is { } wrapped)
        {
            value = wrapped.Get(KnownName.D);
        }

        return value.AsArray() is { } array ? ReadExplicitDestination(array) : null;
    }

    /// <summary>Reads an action dictionary.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action; an <see cref="UnsupportedAction"/> for names the library does not read and for navigation actions that lead nowhere.</returns>
    public PdfAction ReadAction(PdfDictionary action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var navigation = ReadNavigationAction(action);
        return navigation.HasAction ? navigation : ReadExtendedAction(action, action.NameText("S") ?? string.Empty);
    }

    /// <summary>Reads an optional coordinate; null means "unchanged".</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="index">The position.</param>
    /// <returns>The coordinate, or <see langword="null"/>.</returns>
    private static float? Optional(PdfArray array, int index)
    {
        var value = array.Get(index);
        return value.IsNumber ? value.AsSingle() : null;
    }

    /// <summary>Reads the viewer-facing action kinds.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action; no action (a null <c>Value</c>) for other kinds or when it leads nowhere.</returns>
    private PdfAction ReadNavigationAction(PdfDictionary action) => action.GetName(KnownName.S).ToKnownName() switch
    {
        KnownName.GoTo => ReadGoTo(action),
        KnownName.URI => ReadUri(action),
        KnownName.GoToR => ReadRemoteGoTo(action),
        KnownName.Launch => ReadLaunch(action),
        KnownName.GoToE => new EmbeddedGoToAction(action.GetDictionary(KnownName.T)?.GetText(KnownName.N)),
        KnownName.Named => new NamedAction(Objects.Names.GetString(action.GetName(KnownName.N))),
        KnownName.JavaScript => ReadScript(action.Get(KnownName.JS)) is { } script ? new JavaScriptAction(script) : default(PdfAction),
        _ => default,
    };

    /// <summary>Reads one level of the outline and, recursively, the levels below it.</summary>
    /// <param name="first">The first entry at this level.</param>
    /// <param name="depth">The level's depth.</param>
    /// <param name="visited">The entries already read, to stop at loops.</param>
    /// <param name="budget">The entries still allowed.</param>
    /// <returns>The entries.</returns>
    private List<PdfOutlineItem> ReadOutlineLevel(PdfDictionary? first, int depth, HashSet<PdfDictionary> visited, ref int budget)
    {
        var items = new List<PdfOutlineItem>();
        for (var entry = first; entry is not null && budget > 0 && depth < MaxOutlineDepth && visited.Add(entry); entry = entry.GetDictionary(KnownName.Next))
        {
            budget--;
            var children = ReadOutlineLevel(entry.GetDictionary(KnownName.First), depth + 1, visited, ref budget);
            items.Add(new(entry.GetText(KnownName.Title) ?? string.Empty, ReadOutlineTarget(entry), [.. children], entry.GetInteger(KnownName.Count) > 0));
        }

        return items;
    }

    /// <summary>Reads where an outline entry leads: its /Dest, or else its /A action.</summary>
    /// <param name="entry">The outline entry.</param>
    /// <returns>The action.</returns>
    private PdfAction ReadOutlineTarget(PdfDictionary entry)
    {
        if (ResolveDestination(entry.Get(KnownName.Dest)) is { } destination)
        {
            return new GoToAction(destination);
        }

        return entry.GetDictionary(KnownName.A) is { } action ? ReadAction(action) : default;
    }

    /// <summary>Looks up a named destination in the /Names /Dests name tree and then the catalog's /Dests dictionary, as PDFium does.</summary>
    /// <param name="name">The name or string.</param>
    /// <returns>The destination value, or null.</returns>
    private PdfValue FindNamedDestination(PdfValue name)
    {
        var bytes = name.Kind == PdfKind.Name ? Objects.Names.GetSpelling(name.AsName()) : name.AsStringBytes();
        var found = Objects.Resolve(NameTree.Find(Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.Dests), bytes));
        if (!found.IsNull)
        {
            return found;
        }

        return Catalog.GetDictionary(KnownName.Dests) is { } dests ? dests.Get(Objects.Names.Intern(bytes)) : default;
    }

    /// <summary>Reads an explicit destination array: a page then a fit type and its coordinates.</summary>
    /// <param name="array">The destination array.</param>
    /// <returns>The destination, or <see langword="null"/> when its page is not in the document.</returns>
    private PdfDestination? ReadExplicitDestination(PdfArray array)
    {
        var page = array.GetRaw(0);
        var index = page.IsReference ? GetPageIndex(page.AsReference()) : page.AsInt32(-1);
        return (uint)index >= (uint)PageCount ? null : array.GetName(1).ToKnownName() switch
        {
            KnownName.XYZ => new(index, Optional(array, LeftSlot), Optional(array, XyzTopSlot), Optional(array, XyzZoomSlot)),
            KnownName.FitH or KnownName.FitBH => new(index, null, Optional(array, LeftSlot), null),
            KnownName.FitV or KnownName.FitBV => new(index, Optional(array, LeftSlot), null, null),
            KnownName.FitR => new(index, Optional(array, LeftSlot), Optional(array, FitRTopSlot), null),
            _ => new(index, null, null, null),
        };
    }

    /// <summary>Reads a go-to action.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action, or no action when the destination is not in the document.</returns>
    private PdfAction ReadGoTo(PdfDictionary action) =>
        ResolveDestination(action.Get(KnownName.D)) is { } destination ? new GoToAction(destination) : default(PdfAction);

    /// <summary>Reads a remote go-to action: the other file and a page number in it, or page zero.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action, or no action when the file is missing.</returns>
    private PdfAction ReadRemoteGoTo(PdfDictionary action)
    {
        if (ReadFileSpec(action.Get(KnownName.F), out var isUrl) is not { } file)
        {
            return default;
        }

        if (isUrl)
        {
            return new UriAction(file);
        }

        var destination = Objects.Resolve(action.Get(KnownName.D));
        if (destination.Kind is PdfKind.Name or PdfKind.String)
        {
            var named = destination.Kind == PdfKind.Name ? Objects.Names.GetString(destination.AsName()) : PdfText.Decode(destination.AsStringBytes());
            return new RemoteGoToAction(ToPlatformPath(file), 0) { NamedDestination = named };
        }

        var page = destination.AsArray()?.Get(0) ?? default;
        return new RemoteGoToAction(ToPlatformPath(file), Math.Max(0, page.AsInt32(0)));
    }
}
