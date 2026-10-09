// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using HyperPdfLibrary.Document;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Checks the built HyperPDF assemblies, read with System.Reflection.Metadata, for any tie to PDFium: no assembly
/// reference to the PDFium engine and no native module import of the PDFium library.
/// </summary>
public sealed class EngineIndependenceTests
{
    /// <summary>The name part that marks a PDFium assembly or native library.</summary>
    private const string PdfiumName = "pdfium";

    /// <summary>The adapter assembly references no PDFium assembly and imports no PDFium library.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AdapterHasNoPdfiumReference() => await Assert.That(PdfiumReferences(typeof(HyperPdfDocument).Assembly.Location)).IsEmpty();

    /// <summary>The managed engine assembly references no PDFium assembly and imports no PDFium library.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LibraryHasNoPdfiumReference() => await Assert.That(PdfiumReferences(typeof(PdfDocument).Assembly.Location)).IsEmpty();

    /// <summary>The check sees a reference when there is one: this test assembly references the PDFium engine for parity.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckFindsTheTestAssemblysPdfiumReference() =>
        await Assert.That(PdfiumReferences(typeof(EngineIndependenceTests).Assembly.Location)).Contains("PdfViewerLite.Pdfium");

    /// <summary>Lists the assembly references and module imports of an assembly that name PDFium.</summary>
    /// <param name="path">The assembly file.</param>
    /// <returns>The names found.</returns>
    private static List<string> PdfiumReferences(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var found = new List<string>();
        foreach (var handle in metadata.AssemblyReferences)
        {
            AddIfPdfium(found, metadata.GetString(metadata.GetAssemblyReference(handle).Name));
        }

        for (var row = 1; row <= metadata.GetTableRowCount(TableIndex.ModuleRef); row++)
        {
            AddIfPdfium(found, metadata.GetString(metadata.GetModuleReference(MetadataTokens.ModuleReferenceHandle(row)).Name));
        }

        return found;
    }

    /// <summary>Adds a name when it names PDFium.</summary>
    /// <param name="found">The names found.</param>
    /// <param name="name">The name.</param>
    private static void AddIfPdfium(List<string> found, string name)
    {
        if (name.Contains(PdfiumName, StringComparison.OrdinalIgnoreCase))
        {
            found.Add(name);
        }
    }
}
