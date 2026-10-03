// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfViewerLite.TestAssets;

/// <summary>
/// Writes small, deterministic PDF files for tests and benchmarks. Page 1 links to page 3 and to a web address, page 2
/// is landscape, every page carries searchable text, and the outline has one entry per page.
/// </summary>
public static class TestPdf
{
    /// <summary>The portrait page width in points.</summary>
    public static readonly int PortraitWidth = 612;

    /// <summary>The portrait page height in points.</summary>
    public static readonly int PortraitHeight = 792;

    /// <summary>The sentence repeated on each page.</summary>
    public static readonly string Sentence = "The quick brown fox jumps over the lazy dog.";

    /// <summary>The URI linked from page 1.</summary>
    public static readonly string LinkUri = "https://example.com/pdfviewerlite";

    /// <summary>The document title.</summary>
    public static readonly string Title = "PdfViewerLite Test Document";

    /// <summary>The document author.</summary>
    public static readonly string Author = "Glenn Watson";

    /// <summary>The page that is landscape.</summary>
    private const int LandscapePage = 2;

    /// <summary>The page the internal link on page 1 points at.</summary>
    private const int LinkedPage = 3;

    /// <summary>The left margin and the distance of the heading baseline from the top.</summary>
    private const int Margin = 72;

    /// <summary>The heading font size.</summary>
    private const int HeadingSize = 24;

    /// <summary>The body font size.</summary>
    private const int BodySize = 12;

    /// <summary>The distance between body lines.</summary>
    private const int LineHeight = 20;

    /// <summary>The gap before a new paragraph.</summary>
    private const int ParagraphGap = 40;

    /// <summary>How far below the baseline a link rectangle starts.</summary>
    private const int LinkDescent = 5;

    /// <summary>The width of link rectangles.</summary>
    private const int LinkWidth = 260;

    /// <summary>Creates a PDF document.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create(int pageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var font = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var outlines = Reserve(objects);
        var pageIds = new int[pageCount];
        for (var i = 0; i < pageCount; i++)
        {
            pageIds[i] = Reserve(objects);
        }

        for (var i = 0; i < pageCount; i++)
        {
            WritePage(objects, pageIds, i, pages, font);
        }

        var outlineIds = WriteOutline(objects, pageIds, outlines);
        objects[outlines - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Outlines /First {outlineIds[0]} 0 R /Last {outlineIds[^1]} 0 R /Count {outlineIds.Length} >>");

        var kids = new StringBuilder();
        foreach (var id in pageIds)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{id} 0 R ");
        }

        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{kids.ToString().TrimEnd()}] /Count {pageCount} >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {pages} 0 R /Outlines {outlines} 0 R /PageMode /UseOutlines >>");
        var info = Add(objects, $"<< /Title ({Title}) /Author ({Author}) /Creator (PdfViewerLite.TestAssets) /CreationDate (D:20260102030405+10'00') >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>
    /// Creates a one page form: a text field "Name", a check box "Agree" (with checked and unchecked appearances) and a
    /// combo box "Colour" offering Red, Green and Blue.
    /// </summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateForm()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        const string tick = "q 0 g BT /ZaDb 12 Tf 2 3 Td (4) Tj ET Q";
        var zapf = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /ZapfDingbats >>");
        var on = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /XObject /Subtype /Form /BBox [0 0 16 16]
               /Resources << /Font << /ZaDb {{zapf}} 0 R >> >> /Length {{tick.Length}} >>
            stream
            {{tick}}
            endstream
            """));
        var off = Add(objects, "<< /Type /XObject /Subtype /Form /BBox [0 0 16 16] /Length 0 >>\nstream\n\nendstream");
        var name = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 650 300 674] /P {{page}} 0 R /F 4
               /DA (/Helv 12 Tf 0 g) /MK << /BC [0.5 0.5 0.5] >> >>
            """));
        var agree = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 610 88 626] /P {{page}} 0 R /F 4 /V /Off /AS /Off
               /MK << /BC [0.5 0.5 0.5] >> /AP << /N << /Yes {{on}} 0 R /Off {{off}} 0 R >> >> >>
            """));
        var colour = Add(objects, string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T (Colour) /Rect [72 570 200 594] /P {{page}} 0 R /F 4
               /Opt [(Red) (Green) (Blue)] /V (Red) /DA (/Helv 12 Tf 0 g) /MK << /BC [0.5 0.5 0.5] >> >>
            """));
        var content = new StringBuilder();
        AppendText(content, PortraitHeight - Margin, HeadingSize, "Form");
        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PortraitWidth}} {{PortraitHeight}}]
               /Resources << /Font << /F1 {{font}} 0 R >> >> /Contents {{contentId}} 0 R /Annots [{{name}} 0 R {{agree}} 0 R {{colour}} 0 R] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Catalog /Pages {{pages}} 0 R
               /AcroForm << /Fields [{{name}} 0 R {{agree}} 0 R {{colour}} 0 R] /NeedAppearances true /DA (/Helv 12 Tf 0 g)
                            /DR << /Font << /Helv {{font}} 0 R /ZaDb {{zapf}} 0 R >> >> >> >>
            """);
        var info = Add(objects, $"<< /Title (Form) /Author ({Author}) >>");
        return Serialize(objects, catalog, info);
    }

    /// <summary>Writes a PDF to a new temporary file.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The file path.</returns>
    public static string WriteTempFile(int pageCount)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-test-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, Create(pageCount));
        return path;
    }

    /// <summary>Gets the size of a page in points.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public static void GetPageSize(int pageIndex, out int width, out int height)
    {
        var landscape = pageIndex == LandscapePage - 1;
        width = landscape ? PortraitHeight : PortraitWidth;
        height = landscape ? PortraitWidth : PortraitHeight;
    }

    /// <summary>Reserves an object number to be filled in later.</summary>
    /// <param name="objects">The object list.</param>
    /// <returns>The object number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Reserve(List<string> objects) => Add(objects, string.Empty);

    /// <summary>Adds an object.</summary>
    /// <param name="objects">The object list.</param>
    /// <param name="body">The object body.</param>
    /// <returns>The object number.</returns>
    private static int Add(List<string> objects, string body)
    {
        objects.Add(body);
        return objects.Count;
    }

    /// <summary>Writes one page, its content stream and its annotations.</summary>
    /// <param name="objects">The object list.</param>
    /// <param name="pageIds">The page object numbers.</param>
    /// <param name="index">The page index.</param>
    /// <param name="pages">The page tree object number.</param>
    /// <param name="font">The font object number.</param>
    private static void WritePage(List<string> objects, int[] pageIds, int index, int pages, int font)
    {
        GetPageSize(index, out var width, out var height);
        var number = index + 1;
        var baseline = height - Margin;
        var content = new StringBuilder();
        AppendText(content, baseline, HeadingSize, $"Page {number}");
        baseline -= ParagraphGap;
        AppendText(content, baseline, BodySize, Sentence);
        baseline -= LineHeight;
        AppendText(content, baseline, BodySize, $"Unique marker page{number}marker");
        var annotations = string.Empty;
        if (index == 0 && pageIds.Length >= LinkedPage)
        {
            baseline -= ParagraphGap;
            AppendText(content, baseline, BodySize, $"Go to page {LinkedPage}");
            var internalLink = Add(objects, $"<< /Type /Annot /Subtype /Link /Rect [{LinkRect(baseline)}] /Border [0 0 0] /Dest [{pageIds[LinkedPage - 1]} 0 R /XYZ 0 {height} 0] >>");
            baseline -= LineHeight;
            AppendText(content, baseline, BodySize, LinkUri);
            var uriLink = Add(objects, $"<< /Type /Annot /Subtype /Link /Rect [{LinkRect(baseline)}] /Border [0 0 0] /A << /S /URI /URI ({LinkUri}) >> >>");
            annotations = string.Create(CultureInfo.InvariantCulture, $" /Annots [{internalLink} 0 R {uriLink} 0 R]");
        }

        var stream = content.ToString();
        var contentId = Add(objects, string.Create(CultureInfo.InvariantCulture, $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream"));
        objects[pageIds[index] - 1] = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent {pages} 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {contentId} 0 R{annotations} >>");
    }

    /// <summary>Appends a line of text to a content stream.</summary>
    /// <param name="content">The content stream.</param>
    /// <param name="baseline">The baseline y coordinate.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The text.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AppendText(StringBuilder content, int baseline, int size, string text) =>
        content.Append(CultureInfo.InvariantCulture, $"BT /F1 {size} Tf {Margin} {baseline} Td ({text}) Tj ET\n");

    /// <summary>Formats the rectangle of a link covering a line of body text.</summary>
    /// <param name="baseline">The baseline.</param>
    /// <returns>The rectangle as PDF array contents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string LinkRect(int baseline) =>
        string.Create(CultureInfo.InvariantCulture, $"{Margin} {baseline - LinkDescent} {Margin + LinkWidth} {baseline + LineHeight - LinkDescent}");

    /// <summary>Writes the outline entries, one per page.</summary>
    /// <param name="objects">The object list.</param>
    /// <param name="pageIds">The page object numbers.</param>
    /// <param name="parent">The outline root object number.</param>
    /// <returns>The outline entry object numbers.</returns>
    private static int[] WriteOutline(List<string> objects, int[] pageIds, int parent)
    {
        var ids = new int[pageIds.Length];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = Reserve(objects);
        }

        for (var i = 0; i < ids.Length; i++)
        {
            GetPageSize(i, out _, out var height);
            var links = new StringBuilder();
            if (i > 0)
            {
                _ = links.Append(CultureInfo.InvariantCulture, $" /Prev {ids[i - 1]} 0 R");
            }

            if (i < ids.Length - 1)
            {
                _ = links.Append(CultureInfo.InvariantCulture, $" /Next {ids[i + 1]} 0 R");
            }

            objects[ids[i] - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Title (Chapter {i + 1}) /Parent {parent} 0 R /Dest [{pageIds[i]} 0 R /XYZ 0 {height} 0]{links} >>");
        }

        return ids;
    }

    /// <summary>Serialises objects with a cross reference table.</summary>
    /// <param name="objects">The objects.</param>
    /// <param name="root">The catalog object number.</param>
    /// <param name="info">The info dictionary object number.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] Serialize(List<string> objects, int root, int info)
    {
        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new int[objects.Count];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i] = Encoding.ASCII.GetByteCount(output.ToString());
            _ = output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(output.ToString());
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root {root} 0 R /Info {info} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(output.ToString());
    }
}
