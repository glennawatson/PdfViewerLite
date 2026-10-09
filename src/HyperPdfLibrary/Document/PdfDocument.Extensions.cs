// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Developer extensions and unknown dictionary entries.</content>
public sealed partial class PdfDocument
{
    /// <summary>Gets the developer extensions the catalog declares (<c>/Extensions</c>). PDF 2.0 allows an array of dictionaries for one prefix.</summary>
    /// <returns>The extensions in dictionary order.</returns>
    public PdfDeveloperExtension[] GetDeveloperExtensions()
    {
        var extensions = Catalog.GetDictionary(KnownName.Extensions);
        var result = new List<PdfDeveloperExtension>();
        for (var i = 0; extensions is not null && i < extensions.Count; i++)
        {
            var prefix = Objects.Names.GetString(extensions.GetKeyAt(i));
            var value = Objects.Resolve(extensions.GetValueAt(i));
            if (value.AsArray() is { } array)
            {
                for (var j = 0; j < array.Count; j++)
                {
                    AddExtension(result, prefix, array.GetDictionary(j));
                }
            }
            else
            {
                AddExtension(result, prefix, value.AsDictionary());
            }
        }

        return [.. result];
    }

    /// <summary>Finds the catalog and page entries whose keys ISO 32000-2 does not define. An unknown key is data, not an error.</summary>
    /// <returns>The unknown entries, the catalog's first, then each page's.</returns>
    public PdfUnknownEntry[] FindUnknownEntries()
    {
        var scanner = new UnknownEntryScanner(Objects);
        var output = new List<PdfUnknownEntry>();
        scanner.Scan(Catalog, PdfEntryOwner.Catalog, null, output);
        foreach (var page in PageSet.Pages)
        {
            scanner.Scan(page.Dictionary, PdfEntryOwner.Page, page.Index, output);
        }

        return [.. output];
    }

    /// <summary>Finds the unknown entries of one page's dictionary.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The unknown entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public PdfUnknownEntry[] FindUnknownEntries(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var output = new List<PdfUnknownEntry>();
        new UnknownEntryScanner(Objects).Scan(page.Dictionary, PdfEntryOwner.Page, page.Index, output);
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
