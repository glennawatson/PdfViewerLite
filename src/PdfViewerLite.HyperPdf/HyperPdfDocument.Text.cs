// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.HyperPdf;

/// <content>Text from the managed library's text pages, which match PDFium's character indexes.</content>
public sealed partial class HyperPdfDocument : ITextLayoutSource
{
    /// <summary>A typical font's ascent-to-descent height relative to its size.</summary>
    private const float LooseBoxToSize = 1.15F;

    /// <summary>How much larger the drawn size must be before the reported font size is treated as scaled.</summary>
    private const float ScaledTextRatio = 1.5F;

    /// <summary>The user space rectangles of the calling thread's last query, reused so queries allocate only their output.</summary>
    [ThreadStatic]
    private static List<PdfRectangle>? _rectScratch;

    /// <summary>The matches of the calling thread's last search, reused so searches allocate only their output.</summary>
    [ThreadStatic]
    private static List<PdfTextMatch>? _matchScratch;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCharacterCount(int pageIndex) => GetCharacterCountNative(pageIndex);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCharacterIndexAt(int pageIndex, PagePoint point, float tolerance) => GetCharacterIndexAtNative(pageIndex, point, tolerance);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetText(int pageIndex, int start, int count) => GetTextNative(pageIndex, start, count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetTextBounds(int pageIndex, int start, int count, List<PageRect> output) => GetTextBoundsNative(pageIndex, start, count, output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Find(int pageIndex, string query, SearchOptions options, List<TextMatch> output) => FindNative(pageIndex, query, options, output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetCharacters(int pageIndex, List<PageCharacter> output) => GetCharactersNative(pageIndex, output);

    /// <summary>Gets the number of characters on a page, as <see cref="GetCharacterCount"/> will.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The character count; 0 for a missing page.</returns>
    internal int GetCharacterCountNative(int pageIndex) => TextPage(pageIndex)?.CharCount ?? 0;

    /// <summary>Gets the index of the character at a point, as <see cref="GetCharacterIndexAt"/> will.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="point">The point in viewer page space.</param>
    /// <param name="tolerance">The hit tolerance in points.</param>
    /// <returns>The character index, or -1.</returns>
    internal int GetCharacterIndexAtNative(int pageIndex, PagePoint point, float tolerance)
    {
        if (TextPage(pageIndex) is not { } text)
        {
            return -1;
        }

        var user = text.Page.ToUser(new(point.X, point.Y));
        return Math.Max(text.GetIndexAtPosition(user, tolerance, tolerance), -1);
    }

    /// <summary>Gets the text of a run of characters, as <see cref="GetText"/> will.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The text.</returns>
    internal string GetTextNative(int pageIndex, int start, int count) =>
        count <= 0 || start < 0 || TextPage(pageIndex) is not { } text ? string.Empty : text.GetText(start, count);

    /// <summary>Appends the line rectangles covering a run of characters in viewer space, as <see cref="GetTextBounds"/> will.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="output">The list receiving the rectangles.</param>
    internal void GetTextBoundsNative(int pageIndex, int start, int count, List<PageRect> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (count <= 0 || TextPage(pageIndex) is not { } text)
        {
            return;
        }

        var rects = _rectScratch ??= [];
        rects.Clear();
        text.GetRects(start, count, rects);
        AddViewerRects(text, rects, output);
    }

    /// <summary>Finds every occurrence of text on a page, as <see cref="Find"/> will.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">The list receiving the matches.</param>
    internal void FindNative(int pageIndex, string query, SearchOptions options, List<TextMatch> output)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        if (query.Length == 0 || TextPage(pageIndex) is not { } text)
        {
            return;
        }

        var matches = _matchScratch ??= [];
        matches.Clear();
        text.Find(query, ToLibraryOptions(options), matches);
        foreach (var (start, length) in matches)
        {
            output.Add(new(pageIndex, start, length));
        }
    }

    /// <summary>Appends every character of a page in viewer space, as <see cref="GetCharacters"/> will.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the characters.</param>
    internal void GetCharactersNative(int pageIndex, List<PageCharacter> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (TextPage(pageIndex) is not { } text)
        {
            return;
        }

        _ = output.EnsureCapacity(output.Count + text.CharCount);
        foreach (var info in text.Chars)
        {
            var generated = info.IsGenerated;
            var bounds = generated ? default : CharacterBounds(text, info);
            output.Add(new(info.Unicode, bounds, EffectiveSize(info), !generated && info.IsBold, generated));
        }
    }

    /// <summary>Appends the web and email addresses written as text on a page, as PDFium's web links are added to <see cref="GetLinks"/>.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the links.</param>
    internal void GetWebLinksNative(int pageIndex, List<PageLink> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (TextPage(pageIndex) is not { } text)
        {
            return;
        }

        var rects = _rectScratch ??= [];
        var viewer = new List<PageRect>();
        foreach (var link in text.GetWebLinks())
        {
            rects.Clear();
            viewer.Clear();
            text.GetWebLinkRects(link, rects);
            AddViewerRects(text, rects, viewer);
            var target = LinkTarget.ForUri(link.Url);
            foreach (var rect in viewer)
            {
                output.Add(new(rect, target));
            }
        }
    }

    /// <summary>Maps the viewer's search options to the library's.</summary>
    /// <param name="options">The viewer options.</param>
    /// <returns>The library options.</returns>
    private static PdfTextSearchOptions ToLibraryOptions(SearchOptions options)
    {
        var result = PdfTextSearchOptions.None;
        if ((options & SearchOptions.MatchCase) != 0)
        {
            result |= PdfTextSearchOptions.MatchCase;
        }

        if ((options & SearchOptions.WholeWord) != 0)
        {
            result |= PdfTextSearchOptions.WholeWord;
        }

        return result;
    }

    /// <summary>Converts user space rectangles to viewer space.</summary>
    /// <param name="text">The text page.</param>
    /// <param name="rects">The rectangles in user space.</param>
    /// <param name="output">The list receiving the viewer rectangles.</param>
    private static void AddViewerRects(PdfTextPage text, List<PdfRectangle> rects, List<PageRect> output)
    {
        foreach (var rect in rects)
        {
            output.Add(LinkTargets.ToPageRect(text.Page.ToViewerRectangle(rect)));
        }
    }

    /// <summary>
    /// Gets a character's box from its advance and the font's ascent and descent, so narrow letters keep their side
    /// bearings; the outline box is the fallback, as the PDFium document does.
    /// </summary>
    /// <param name="text">The text page.</param>
    /// <param name="info">The character.</param>
    /// <returns>The box in viewer space.</returns>
    private static PageRect CharacterBounds(PdfTextPage text, in PdfTextChar info)
    {
        var box = info.LooseBox.Right > info.LooseBox.Left ? info.LooseBox : info.Box;
        return LinkTargets.ToPageRect(text.Page.ToViewerRectangle(box));
    }

    /// <summary>Gets the size a character is drawn at: the box height when the text matrix scales a small font size up.</summary>
    /// <param name="info">The character.</param>
    /// <returns>The size in points.</returns>
    private static float EffectiveSize(in PdfTextChar info)
    {
        if (info.IsGenerated)
        {
            return info.FontSize;
        }

        var drawn = info.LooseBox.Height / LooseBoxToSize;
        return drawn > info.FontSize * ScaledTextRatio ? drawn : info.FontSize;
    }

    /// <summary>Gets a page's text from the managed library.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The text page, or <see langword="null"/> when the document is closed or the page does not exist.</returns>
    private PdfTextPage? TextPage(int pageIndex) =>
        IsDisposed || (uint)pageIndex >= (uint)PageCount ? null : _document.GetTextPage(pageIndex);
}
