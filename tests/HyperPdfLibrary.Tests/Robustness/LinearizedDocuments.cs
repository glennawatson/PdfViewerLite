// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>
/// Builds a two page linearized file: the linearization dictionary and first-page cross-reference section at the start
/// (with /Prev pointing at the main section), a hint stream, the first page's objects, then the rest and the main section.
/// The last startxref points at the first-page section, as linearized writers do.
/// </summary>
internal static class LinearizedDocuments
{
    /// <summary>The number of pages.</summary>
    internal const int PageCount = 2;

    /// <summary>The object number of the linearization dictionary.</summary>
    private const int LinearizationNumber = 7;

    /// <summary>The object number of the hint stream.</summary>
    private const int HintNumber = 8;

    /// <summary>The object number of the catalog.</summary>
    private const int CatalogNumber = 4;

    /// <summary>The object number of the first page.</summary>
    private const int FirstPageNumber = 5;

    /// <summary>The object number of the first page's content.</summary>
    private const int FirstContentNumber = 6;

    /// <summary>The object number of the page tree root.</summary>
    private const int PagesNumber = 1;

    /// <summary>The object number of the second page.</summary>
    private const int SecondPageNumber = 2;

    /// <summary>The object number of the second page's content.</summary>
    private const int SecondContentNumber = 3;

    /// <summary>The object number of the main cross-reference stream.</summary>
    private const int MainStreamNumber = 9;

    /// <summary>The object number of the first-page cross-reference stream.</summary>
    private const int FirstStreamNumber = 10;

    /// <summary>The number of objects including the free head.</summary>
    private const int ObjectCount = 11;

    /// <summary>The number of bytes in a hint stream.</summary>
    private const int HintLength = 24;

    /// <summary>The number of bytes in a cross-reference stream row: type, offset, generation.</summary>
    private const int RowLength = 7;

    /// <summary>The offset of the generation in a row.</summary>
    private const int GenerationOffset = 5;

    /// <summary>The generation of the free head entry.</summary>
    private const int FreeGeneration = 65_535;

    /// <summary>Gets the content of the first page.</summary>
    internal static string FirstContent => "BT (First) Tj ET";

    /// <summary>Gets the content of the second page.</summary>
    internal static string SecondContent => "BT (Second) Tj ET";

    /// <summary>Builds the file.</summary>
    /// <param name="xrefStreams">Whether the two cross-reference sections are streams instead of tables.</param>
    /// <returns>The file.</returns>
    internal static byte[] Create(bool xrefStreams)
    {
        var measured = new Layout();
        _ = Assemble(xrefStreams, new(), measured);
        var final = new Layout();
        return Assemble(xrefStreams, measured, final);
    }

    /// <summary>Writes the file once, reading the offsets of the previous pass and recording this pass's offsets.</summary>
    /// <param name="xrefStreams">Whether the sections are streams.</param>
    /// <param name="previous">The offsets measured by the previous pass.</param>
    /// <param name="current">Receives the offsets of this pass.</param>
    /// <returns>The file.</returns>
    private static byte[] Assemble(bool xrefStreams, Layout previous, Layout current)
    {
        using var output = new MemoryStream();
        Write(output, "%PDF-1.7\n");
        current.Offsets[LinearizationNumber] = output.Position;
        var linearization = new StringBuilder();
        _ = linearization.Append(CultureInfo.InvariantCulture, $"{LinearizationNumber} 0 obj\n<< /Linearized 1 /L {previous.Length:D10} ");
        _ = linearization.Append(CultureInfo.InvariantCulture, $"/H [{previous.HintOffset:D10} {previous.HintObjectLength:D10}] /O {FirstPageNumber} ");
        _ = linearization.Append(CultureInfo.InvariantCulture, $"/E {previous.EndFirst:D10} /N {PageCount} /T {previous.MainXref:D10} >>\nendobj\n");
        Write(output, linearization.ToString());
        current.FirstXref = output.Position;
        current.Offsets[FirstStreamNumber] = output.Position;
        WriteFirstSection(output, xrefStreams, previous);
        current.HintOffset = output.Position;
        current.Offsets[HintNumber] = output.Position;
        WriteObject(output, HintNumber, $"<< /S 12 /Length {HintLength} >>\nstream\n{new string('h', HintLength)}\nendstream");
        current.HintObjectLength = output.Position - current.HintOffset;
        WriteFirstObjects(output, current);
        current.EndFirst = output.Position;
        WriteMainObjects(output, current);
        current.MainXref = output.Position;
        WriteMainSection(output, xrefStreams, previous);
        Write(output, string.Create(CultureInfo.InvariantCulture, $"startxref\n{current.FirstXref}\n%%EOF\n"));
        current.Length = output.Length;
        return output.ToArray();
    }

    /// <summary>Writes the first-page cross-reference section and its trailer, ending with the start-of-file startxref.</summary>
    /// <param name="output">The output.</param>
    /// <param name="xrefStreams">Whether to write a stream.</param>
    /// <param name="previous">The previous pass's offsets.</param>
    private static void WriteFirstSection(MemoryStream output, bool xrefStreams, Layout previous)
    {
        if (xrefStreams)
        {
            var rows = new MemoryStream();
            for (var number = CatalogNumber; number <= FirstStreamNumber; number++)
            {
                AppendRow(rows, 1, previous.Offsets.GetValueOrDefault(number), 0);
            }

            var entries = string.Create(
                CultureInfo.InvariantCulture,
                $"/Type /XRef /Size {ObjectCount} /W [1 4 2] /Index [{CatalogNumber} {FirstStreamNumber - CatalogNumber + 1}] /Root {CatalogNumber} 0 R /Prev {previous.MainXref:D10}");
            WriteObject(output, FirstStreamNumber, StreamObject(entries, rows.ToArray()));
            Write(output, "startxref\n0\n%%EOF\n");
            return;
        }

        var table = new StringBuilder();
        _ = table.Append(CultureInfo.InvariantCulture, $"xref\n{CatalogNumber} {HintNumber - CatalogNumber + 1}\n");
        for (var number = CatalogNumber; number <= HintNumber; number++)
        {
            _ = table.Append(CultureInfo.InvariantCulture, $"{previous.Offsets.GetValueOrDefault(number):D10} 00000 n \n");
        }

        _ = table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {ObjectCount} /Root {CatalogNumber} 0 R /Prev {previous.MainXref:D10} >>\nstartxref\n0\n%%EOF\n");
        Write(output, table.ToString());
    }

    /// <summary>Writes the objects of the first page, and records their offsets.</summary>
    /// <param name="output">The output.</param>
    /// <param name="current">Receives the offsets.</param>
    private static void WriteFirstObjects(MemoryStream output, Layout current)
    {
        current.Offsets[CatalogNumber] = output.Position;
        WriteObject(output, CatalogNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {PagesNumber} 0 R >>"));
        current.Offsets[FirstPageNumber] = output.Position;
        WriteObject(
            output,
            FirstPageNumber,
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 200 100] /Resources << >> /Contents {FirstContentNumber} 0 R >>"));
        current.Offsets[FirstContentNumber] = output.Position;
        WriteObject(output, FirstContentNumber, StreamObject(string.Empty, Encoding.Latin1.GetBytes(FirstContent)));
    }

    /// <summary>Writes the page tree and the second page, and records their offsets.</summary>
    /// <param name="output">The output.</param>
    /// <param name="current">Receives the offsets.</param>
    private static void WriteMainObjects(MemoryStream output, Layout current)
    {
        current.Offsets[PagesNumber] = output.Position;
        WriteObject(output, PagesNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{FirstPageNumber} 0 R {SecondPageNumber} 0 R] /Count {PageCount} >>"));
        current.Offsets[SecondPageNumber] = output.Position;
        WriteObject(
            output,
            SecondPageNumber,
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 100 200] /Resources << >> /Contents {SecondContentNumber} 0 R >>"));
        current.Offsets[SecondContentNumber] = output.Position;
        WriteObject(output, SecondContentNumber, StreamObject(string.Empty, Encoding.Latin1.GetBytes(SecondContent)));
    }

    /// <summary>Writes the main cross-reference section: objects 0 to 3 (and the stream itself, for a stream).</summary>
    /// <param name="output">The output.</param>
    /// <param name="xrefStreams">Whether to write a stream.</param>
    /// <param name="previous">The previous pass's offsets.</param>
    private static void WriteMainSection(MemoryStream output, bool xrefStreams, Layout previous)
    {
        if (xrefStreams)
        {
            var rows = new MemoryStream();
            AppendRow(rows, 0, 0, FreeGeneration);
            for (var number = PagesNumber; number <= SecondContentNumber; number++)
            {
                AppendRow(rows, 1, previous.Offsets.GetValueOrDefault(number), 0);
            }

            AppendRow(rows, 1, previous.MainXref, 0);
            WriteObject(output, MainStreamNumber, StreamObject($"/Type /XRef /Size {ObjectCount} /W [1 4 2] /Index [0 {SecondContentNumber + 1} {MainStreamNumber} 1]", rows.ToArray()));
            return;
        }

        var table = new StringBuilder("xref\n0 4\n0000000000 65535 f \n");
        for (var number = PagesNumber; number <= SecondContentNumber; number++)
        {
            _ = table.Append(CultureInfo.InvariantCulture, $"{previous.Offsets.GetValueOrDefault(number):D10} 00000 n \n");
        }

        _ = table.Append("trailer\n<< /Size 4 >>\n");
        Write(output, table.ToString());
    }

    /// <summary>Appends one cross-reference stream row with fields of one, four and two bytes.</summary>
    /// <param name="rows">The rows so far.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="generation">The generation.</param>
    private static void AppendRow(MemoryStream rows, byte type, long offset, int generation)
    {
        Span<byte> row = stackalloc byte[RowLength];
        row[0] = type;
        BinaryPrimitives.WriteUInt32BigEndian(row[1..], (uint)offset);
        BinaryPrimitives.WriteUInt16BigEndian(row[GenerationOffset..], (ushort)generation);
        rows.Write(row);
    }

    /// <summary>Writes text.</summary>
    /// <param name="output">The output.</param>
    /// <param name="text">The text, one byte per character.</param>
    private static void Write(MemoryStream output, string text) => output.Write(Encoding.Latin1.GetBytes(text));

    /// <summary>Writes an indirect object.</summary>
    /// <param name="output">The output.</param>
    /// <param name="number">The object number.</param>
    /// <param name="body">The object body as text.</param>
    private static void WriteObject(MemoryStream output, int number, string body) =>
        Write(output, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n"));

    /// <summary>Builds a stream object body.</summary>
    /// <param name="entries">The extra dictionary entries.</param>
    /// <param name="data">The data.</param>
    /// <returns>The body.</returns>
    private static string StreamObject(string entries, byte[] data) =>
        string.Create(CultureInfo.InvariantCulture, $"<< {entries} /Length {data.Length} >>\nstream\n{Encoding.Latin1.GetString(data)}\nendstream");

    /// <summary>The offsets one pass measured.</summary>
    private sealed class Layout
    {
        /// <summary>Gets the offset of each object.</summary>
        public Dictionary<int, long> Offsets { get; } = [];

        /// <summary>Gets or sets the offset of the first-page cross-reference section.</summary>
        public long FirstXref { get; set; }

        /// <summary>Gets or sets the offset of the main cross-reference section.</summary>
        public long MainXref { get; set; }

        /// <summary>Gets or sets the offset after the first page's objects.</summary>
        public long EndFirst { get; set; }

        /// <summary>Gets or sets the offset of the hint stream.</summary>
        public long HintOffset { get; set; }

        /// <summary>Gets or sets the length of the hint stream object.</summary>
        public long HintObjectLength { get; set; }

        /// <summary>Gets or sets the file length.</summary>
        public long Length { get; set; }
    }
}
