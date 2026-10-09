// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.TextLayer;

/// <summary>
/// Puts a text layer on a page, copy-on-write: the page, its resources and its content array are copied, changed and
/// put back into the document's objects, so readers never see a half-changed page.
/// </summary>
internal static class PdfTextLayerPage
{
    /// <summary>The longest name: the prefix, then the digits of an int.</summary>
    private const int MaxNameLength = 16;

    /// <summary>The entries of a small dictionary.</summary>
    private const int SmallEntries = 4;

    /// <summary>Gets the prefix of a text layer font's resource name.</summary>
    private static ReadOnlySpan<byte> NamePrefix => "OcrF"u8;

    /// <summary>Gets the content that opens the graphics state wrapped round the existing content.</summary>
    private static ReadOnlySpan<byte> Open => "q\n"u8;

    /// <summary>Gets the content that closes the graphics state wrapped round the existing content.</summary>
    private static ReadOnlySpan<byte> Close => "\nQ\n"u8;

    /// <summary>Chooses a resource name for each font that the page does not already use.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="count">The number of fonts.</param>
    /// <returns>The names, as bytes without the slash.</returns>
    internal static byte[][] ChooseNames(PdfObjectStore store, PdfPage page, int count)
    {
        var existing = Resources(store, page)?.GetDictionary(KnownName.Font);
        var names = new byte[count][];
        Span<byte> buffer = stackalloc byte[MaxNameLength];
        NamePrefix.CopyTo(buffer);
        var number = 1;
        for (var i = 0; i < count; i++)
        {
            int length;
            do
            {
                _ = number.TryFormat(buffer[NamePrefix.Length..], out var digits);
                length = NamePrefix.Length + digits;
                number++;
            }
            while (existing is not null && existing.ContainsKey(store.Names.Intern(buffer[..length])));

            names[i] = buffer[..length].ToArray();
        }

        return names;
    }

    /// <summary>Adds the fonts, wraps the existing content and appends the text layer.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="names">The resource name of each font.</param>
    /// <param name="used">Which fonts the content uses.</param>
    /// <param name="content">The text layer's content.</param>
    /// <returns><see langword="true"/> when the page was changed.</returns>
    internal static bool Install(PdfObjectStore store, PdfPage page, ReadOnlySpan<IPdfTextLayerFont> fonts, byte[][] names, bool[] used, ReadOnlySpan<byte> content)
    {
        var current = PdfPageAnnotations.GetPageDictionary(store, page);
        var copy = current.Clone();
        copy.Set(KnownName.Resources, PdfValue.FromDictionary(WithFonts(store, Resources(store, page), fonts, names, used)));
        copy.Set(KnownName.Contents, PdfValue.FromArray(WrapContents(store, current, content)));
        store.Replace(page.Id, PdfValue.FromDictionary(copy));
        return true;
    }

    /// <summary>Gets the page's resources as they are now, including any inherited from the page tree.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The resources, or <see langword="null"/> when there are none.</returns>
    private static PdfDictionary? Resources(PdfObjectStore store, PdfPage page) =>
        PdfPageAnnotations.GetPageDictionary(store, page).GetDictionary(KnownName.Resources) ?? page.Resources;

    /// <summary>Copies the resources with the used fonts added.</summary>
    /// <param name="store">The document.</param>
    /// <param name="resources">The resources, or <see langword="null"/>.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="names">The resource name of each font.</param>
    /// <param name="used">Which fonts the content uses.</param>
    /// <returns>The new resources.</returns>
    private static PdfDictionary WithFonts(PdfObjectStore store, PdfDictionary? resources, ReadOnlySpan<IPdfTextLayerFont> fonts, byte[][] names, bool[] used)
    {
        var copy = resources?.Clone() ?? new PdfDictionary(store, SmallEntries);
        var table = copy.GetDictionary(KnownName.Font)?.Clone() ?? new PdfDictionary(store, fonts.Length);
        for (var i = 0; i < fonts.Length; i++)
        {
            if (used[i])
            {
                table.Set(store.Names.Intern(names[i]), fonts[i].Resource);
            }
        }

        copy.Set(KnownName.Font, PdfValue.FromDictionary(table));
        return copy;
    }

    /// <summary>Builds a content array: the existing content between a save and a restore, then the new content.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page dictionary as it is now.</param>
    /// <param name="content">The text layer's content.</param>
    /// <returns>The array.</returns>
    private static PdfArray WrapContents(PdfObjectStore store, PdfDictionary page, ReadOnlySpan<byte> content)
    {
        var raw = page.GetRaw(KnownName.Contents);
        var existing = page.Get(KnownName.Contents).AsArray();
        var array = new PdfArray(store, (existing?.Count ?? 1) + SmallEntries);
        array.Add(Plain(store, Open));
        if (existing is not null)
        {
            foreach (var item in existing.Items)
            {
                array.Add(item);
            }
        }
        else if (page.Get(KnownName.Contents).AsStream() is not null)
        {
            array.Add(raw);
        }

        array.Add(Plain(store, Close));
        array.Add(Compressed(store, content));
        return array;
    }

    /// <summary>Adds a stream holding some content as it is.</summary>
    /// <param name="store">The document.</param>
    /// <param name="content">The content.</param>
    /// <returns>A reference to the stream.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfValue Plain(PdfObjectStore store, ReadOnlySpan<byte> content) =>
        PdfValue.FromReference(store.Add(PdfValue.FromStream(new(new PdfDictionary(store, 1), content.ToArray()))));

    /// <summary>Adds a Flate-compressed stream holding some content.</summary>
    /// <param name="store">The document.</param>
    /// <param name="content">The content.</param>
    /// <returns>A reference to the stream.</returns>
    private static PdfValue Compressed(PdfObjectStore store, ReadOnlySpan<byte> content)
    {
        var dictionary = new PdfDictionary(store, 1);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(content, ref compressed);
            return PdfValue.FromReference(store.Add(PdfValue.FromStream(new(dictionary, compressed.ToArray()))));
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
