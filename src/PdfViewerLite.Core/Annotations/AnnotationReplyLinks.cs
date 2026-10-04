// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using PdfViewerLite.Core.Signatures.Signing;

namespace PdfViewerLite.Core.Annotations;

/// <summary>
/// Links replies to the comments they answer when a document is saved. A reply records the comment it answers by
/// name (<c>/NM</c>) while it is being edited, under <see cref="PendingKey"/>, because PDFium cannot write the
/// reference PDF needs; on save, an incremental update replaces that with the standard <c>/IRT</c> reference and
/// <c>/RT /R</c>, so every reader shows the thread.
/// </summary>
public static class AnnotationReplyLinks
{
    /// <summary>Gets the key holding the name of the comment a reply answers until the file is saved.</summary>
    public static string PendingKey => Encoding.ASCII.GetString(PendingName);

    /// <summary>Gets the pending key's name without its slash.</summary>
    private static ReadOnlySpan<byte> PendingName => "PVLInReplyTo"u8;

    /// <summary>Links every pending reply in a saved file.</summary>
    /// <param name="file">The saved file.</param>
    /// <returns>The file with an incremental update linking the replies, or the same bytes when there is nothing to link or the file cannot be read.</returns>
    public static byte[] Link(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.AsSpan().IndexOf(PendingName) < 0)
        {
            return file;
        }

        try
        {
            var structure = PdfReader.Read(file);
            var root = PdfPages.ReadReference(structure.Trailer, "Root"u8);
            var catalog = PdfReader.GetObject(structure, root);
            var objects = new SortedDictionary<int, string>();
            var size = 0;
            foreach (var number in structure.Entries.Keys)
            {
                size = Math.Max(size, number + 1);
            }

            var count = PageCount(structure, catalog);
            for (var page = 0; page < count; page++)
            {
                var pageNumber = PdfPages.FindPage(structure, catalog, page);
                LinkPage(structure, pageNumber, objects, ref size);
            }

            return objects.Count == 0 ? file : [.. file, .. PdfUpdateWriter.Serialize(structure, objects, root, size)];
        }
        catch (InvalidDataException)
        {
            return file;
        }
    }

    /// <summary>Reads the number of pages from the page tree's root.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="catalog">The catalog.</param>
    /// <returns>The page count.</returns>
    private static int PageCount(PdfStructure structure, ReadOnlyMemory<byte> catalog)
    {
        var pages = PdfReader.GetObject(structure, PdfPages.ReadReference(catalog.Span, "Pages"u8));
        var countAt = PdfSyntax.FindKey(pages.Span, 0, "Count"u8);
        return countAt >= 0 && PdfSyntax.ReadLong(pages.Span, countAt, out var count) >= 0 ? (int)count : 0;
    }

    /// <summary>
    /// Links the pending replies on one page. PDFium writes new annotations inline in the page's <c>/Annots</c> array,
    /// and <c>/IRT</c> needs a reference, so on a page with replies every inline annotation becomes an object of its own.
    /// </summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="pageNumber">The page's object number.</param>
    /// <param name="objects">Receives the rewritten objects.</param>
    /// <param name="size">The next free object number, moved past any objects made.</param>
    private static void LinkPage(PdfStructure structure, int pageNumber, SortedDictionary<int, string> objects, ref int size)
    {
        var page = PdfReader.GetObject(structure, pageNumber);
        var annotsAt = PdfSyntax.FindKey(page.Span, 0, "Annots"u8);
        if (annotsAt < 0)
        {
            return;
        }

        var annots = PdfReader.Resolve(structure, page, annotsAt);
        var items = ReadAnnotations(structure, annots.Span);
        if (!items.Exists(static item => ReadText(item.Body, PendingName).Length > 0))
        {
            return;
        }

        // Give every inline annotation a number, and learn every annotation's name.
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Number < 0)
            {
                items[i] = (size, items[i].Body, true);
                size++;
            }

            if (ReadText(items[i].Body, "NM"u8) is { Length: > 0 } name)
            {
                names[name] = items[i].Number;
            }
        }

        var references = new StringBuilder("[");
        foreach (var (number, body, inline) in items)
        {
            _ = references.Append(CultureInfo.InvariantCulture, $"{number} 0 R ");
            var parent = ReadText(body, PendingName);
            if (parent.Length > 0 && names.TryGetValue(parent, out var parentNumber))
            {
                objects[number] = Linked(body, parentNumber);
            }
            else if (inline)
            {
                objects[number] = Encoding.Latin1.GetString(body.Span);
            }
        }

        var array = references.Append(']').ToString();
        if (PdfSyntax.TryReadReference(page.Span, annotsAt, out var arrayNumber))
        {
            objects[arrayNumber] = array;
        }
        else
        {
            objects[pageNumber] = PdfEditing.AddEntry(PdfEditing.RemoveKey(page.Span, "Annots"u8), $"/Annots {array}");
        }
    }

    /// <summary>Reads a page's annotations: referenced ones with their numbers, inline ones with -1.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="annots">The <c>/Annots</c> array.</param>
    /// <returns>The annotations in order.</returns>
    private static List<(int Number, ReadOnlyMemory<byte> Body, bool Inline)> ReadAnnotations(PdfStructure structure, ReadOnlySpan<byte> annots)
    {
        var items = new List<(int Number, ReadOnlyMemory<byte> Body, bool Inline)>();
        var copy = annots.ToArray();
        var index = PdfSyntax.SkipSpace(annots, 1);
        while (index < annots.Length && annots[index] != (byte)']')
        {
            if (PdfSyntax.TryReadReference(annots, index, out var number))
            {
                items.Add((number, PdfReader.GetObject(structure, number), false));

                // A reference is three tokens: the number, the generation and R.
                var generation = PdfSyntax.SkipSpace(annots, PdfSyntax.TokenEnd(annots, index));
                var reference = PdfSyntax.SkipSpace(annots, PdfSyntax.TokenEnd(annots, generation));
                index = PdfSyntax.SkipSpace(annots, reference + 1);
                continue;
            }

            var end = PdfSyntax.ValueEnd(annots, index);
            if (annots[index] == (byte)'<')
            {
                items.Add((-1, copy.AsMemory(index, end - index), true));
            }

            index = PdfSyntax.SkipSpace(annots, end);
        }

        return items;
    }

    /// <summary>Rewrites a reply with a standard reference to its comment in place of the pending name.</summary>
    /// <param name="body">The reply's dictionary.</param>
    /// <param name="parentNumber">The comment's object number.</param>
    /// <returns>The rewritten dictionary.</returns>
    private static string Linked(ReadOnlyMemory<byte> body, int parentNumber)
    {
        var withoutPending = Encoding.Latin1.GetBytes(PdfEditing.RemoveKey(body.Span, PendingName));
        var withoutOld = PdfEditing.RemoveKey(Encoding.Latin1.GetBytes(PdfEditing.RemoveKey(withoutPending, "IRT"u8)), "RT"u8);
        return PdfEditing.AddEntry(withoutOld, string.Create(CultureInfo.InvariantCulture, $"/IRT {parentNumber} 0 R /RT /R"));
    }

    /// <summary>Reads a text string value of a dictionary.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text, or an empty string.</returns>
    private static string ReadText(ReadOnlyMemory<byte> dictionary, ReadOnlySpan<byte> key)
    {
        var at = PdfSyntax.FindKey(dictionary.Span, 0, key);
        return at < 0 ? string.Empty : PdfText.Decode(dictionary.Span[at..PdfSyntax.ValueEnd(dictionary.Span, at)]);
    }
}
