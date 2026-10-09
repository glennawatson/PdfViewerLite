// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Interchange;

/// <summary>Writes FDF files (ISO 32000-1, 12.7.7): a header, the objects, and a trailer, with no cross-reference table.</summary>
public static class FdfWriter
{
    /// <summary>Gets the header: the FDF signature and a comment of high bytes that marks the file as binary.</summary>
    private static ReadOnlySpan<byte> Header => "%FDF-1.2\n%âãÏÓ\n"u8;

    /// <summary>Gets the trailer up to the catalog's reference.</summary>
    private static ReadOnlySpan<byte> TrailerStart => "trailer\n<< /Root 1 0 R >>\n%%EOF\n"u8;

    /// <summary>Writes the data as an FDF file.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The file's bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public static byte[] Write(PdfInterchangeData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var names = new PdfNameTable();
        var objects = new FdfObjects();
        var catalog = new PdfDictionary(null, 1);
        catalog.Set(names.Intern("FDF"), PdfValue.FromDictionary(FdfDocumentBuilder.Build(data, names, objects)));
        using var writer = new PdfObjectWriter(names);
        writer.WriteRaw(Header);
        writer.WriteIndirectObject(new(1, 0), PdfValue.FromDictionary(catalog));
        for (var i = 0; i < objects.Count; i++)
        {
            writer.WriteIndirectObject(FdfObjects.IdOf(i), objects[i]);
        }

        writer.WriteRaw(TrailerStart);
        return writer.ToArray();
    }
}
