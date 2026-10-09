// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Link annotations.</content>
public sealed partial class PdfDocument
{
    /// <summary>Gets a page's link annotations. A link's action wins over its /Dest, as in PDFium.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The links that lead somewhere.</returns>
    public IReadOnlyList<PdfLink> GetLinks(int pageIndex)
    {
        // Read the page set once: the pages and the link cache come from the same snapshot, so a concurrent edit cannot mix them.
        var set = PageSet;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)pageIndex, (uint)set.Pages.Length, nameof(pageIndex));
        var links = Volatile.Read(ref set.Links[pageIndex]);
        if (links is not null)
        {
            return links;
        }

        links = ReadLinks(set.Pages[pageIndex]);
        Volatile.Write(ref set.Links[pageIndex], links);
        return links;
    }

    /// <summary>Reads the link annotations of a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The links.</returns>
    private PdfLink[] ReadLinks(PdfPage page)
    {
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        if (annotations is null)
        {
            return [];
        }

        var links = new List<PdfLink>();
        for (var i = 0; i < annotations.Count; i++)
        {
            var annotation = annotations.GetDictionary(i);
            if (annotation is null || !annotation.IsName(KnownName.Subtype, KnownName.Link) || !annotation.TryGetRectangle(KnownName.Rect, out var bounds))
            {
                continue;
            }

            var action = ReadLinkAction(annotation);
            if (action.Value is not null)
            {
                links.Add(new(bounds, action));
            }
        }

        return [.. links];
    }

    /// <summary>Reads where a link leads: its action, or else its /Dest.</summary>
    /// <param name="annotation">The link annotation.</param>
    /// <returns>The action.</returns>
    private PdfAction ReadLinkAction(PdfDictionary annotation)
    {
        if (annotation.GetDictionary(KnownName.A) is { } dictionary)
        {
            var action = ReadAction(dictionary);
            if (action.IsNavigation)
            {
                return action;
            }

            // A non-navigation action never hides the link's /Dest; an unsupported one is not a link at all.
            if (ResolveDestination(annotation.Get(KnownName.Dest)) is { } fallback)
            {
                return new GoToAction(fallback);
            }

            return action.Value is UnsupportedAction ? default : action;
        }

        return ResolveDestination(annotation.Get(KnownName.Dest)) is { } destination ? new GoToAction(destination) : default(PdfAction);
    }
}
