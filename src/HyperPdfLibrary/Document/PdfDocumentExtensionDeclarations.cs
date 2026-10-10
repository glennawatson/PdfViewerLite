// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document extension declarations.</summary>
public static class PdfDocumentExtensionDeclarations
{
    /// <summary>Gets the developer extensions the catalog declares (<c>/Extensions</c>). PDF 2.0 allows an array of dictionaries for one prefix.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The extensions in dictionary order.</returns>
    public static PdfDeveloperExtension[] GetDeveloperExtensions(PdfDocument document)
    {
        var extensions = document.Catalog.GetDictionary(KnownName.Extensions);
        var result = new List<PdfDeveloperExtension>();
        for (var i = 0; extensions is not null && i < extensions.Count; i++)
        {
            var prefix = document.Objects.Names.GetString(extensions.GetKeyAt(i));
            var value = StoreReading.Resolve(document.Objects, extensions.GetValueAt(i));
            if (value.AsArray() is { } array)
            {
                for (var j = 0; j < array.Count; j++)
                {
                    PdfDocumentExtensionDeclarations.AddExtension(result, prefix, array.GetDictionary(j));
                }
            }
            else
            {
                PdfDocumentExtensionDeclarations.AddExtension(result, prefix, value.AsDictionary());
            }
        }

        return [.. result];
    }

    /// <summary>Finds the catalog and page entries whose keys ISO 32000-2 does not define. An unknown key is data, not an error.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The unknown entries, the catalog's first, then each page's.</returns>
    public static PdfUnknownEntry[] FindUnknownEntries(PdfDocument document)
    {
        var scanner = new UnknownEntryScanner(document.Objects);
        var output = new List<PdfUnknownEntry>();
        scanner.Scan(document.Catalog, PdfEntryOwner.Catalog, null, output);
        foreach (var page in PdfDocumentPages.GetPageSet(document).Pages)
        {
            scanner.Scan(page.Dictionary, PdfEntryOwner.Page, page.Index, output);
        }

        return [.. output];
    }

    /// <summary>Finds the unknown entries of one page's dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The unknown entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PdfUnknownEntry[] FindUnknownEntries(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var output = new List<PdfUnknownEntry>();
        new UnknownEntryScanner(document.Objects).Scan(page.Dictionary, PdfEntryOwner.Page, page.Index, output);
        return [.. output];
    }

    /// <summary>Adds one extension dictionary.</summary>
    /// <param name="result">The result list.</param>
    /// <param name="prefix">The developer prefix.</param>
    /// <param name="extension">The dictionary, or null.</param>
    private static void AddExtension(List<PdfDeveloperExtension> result, string prefix, PdfDictionary? extension)
    {
        if (extension is not null)
        {
            result.Add(new(prefix, extension.NameText("BaseVersion") ?? extension.Text("BaseVersion") ?? string.Empty, extension.Int("ExtensionLevel", 0), extension.Text("URL")));
        }
    }
}
