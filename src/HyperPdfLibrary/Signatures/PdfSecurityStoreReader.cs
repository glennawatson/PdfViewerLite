// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Reads the document security store (/DSS) and its /VRI entries.</summary>
internal static class PdfSecurityStoreReader
{
    /// <summary>The most entries read from one array or the /VRI dictionary.</summary>
    private const int MaxEntries = 4096;

    /// <summary>Gets the /Certs key.</summary>
    private static ReadOnlySpan<byte> CertsKey => "Certs"u8;

    /// <summary>Gets the /OCSPs key.</summary>
    private static ReadOnlySpan<byte> OcspsKey => "OCSPs"u8;

    /// <summary>Gets the /CRLs key.</summary>
    private static ReadOnlySpan<byte> CrlsKey => "CRLs"u8;

    /// <summary>Gets the /OCSP key of a /VRI entry.</summary>
    private static ReadOnlySpan<byte> OcspKey => "OCSP"u8;

    /// <summary>Gets the /CRL key of a /VRI entry.</summary>
    private static ReadOnlySpan<byte> CrlKey => "CRL"u8;

    /// <summary>Gets the /TS key of a /VRI entry.</summary>
    private static ReadOnlySpan<byte> TimestampKey => "TS"u8;

    /// <summary>Reads the store.</summary>
    /// <param name="catalog">The document catalog.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The store, or <see cref="PdfSecurityStore.Empty"/> when the document has none.</returns>
    internal static PdfSecurityStore Read(PdfDictionary catalog, PdfNameTable names) =>
        catalog.GetDictionary(KnownName.DSS) is not { } dss
            ? PdfSecurityStore.Empty
            : new(
                Streams(dss.GetArray(names.Intern(CertsKey))),
                Streams(dss.GetArray(names.Intern(OcspsKey))),
                Streams(dss.GetArray(names.Intern(CrlsKey))),
                ReadVri(dss.GetDictionary(KnownName.VRI), names));

    /// <summary>Reads the /VRI entries.</summary>
    /// <param name="vri">The /VRI dictionary, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The entries by upper-case key.</returns>
    private static Dictionary<string, PdfValidationRelatedInfo> ReadVri(PdfDictionary? vri, PdfNameTable names)
    {
        var result = new Dictionary<string, PdfValidationRelatedInfo>(StringComparer.Ordinal);
        for (var i = 0; vri is not null && i < vri.Count && i < MaxEntries; i++)
        {
            if (vri.Get(vri.GetKeyAt(i)).AsDictionary() is not { } entry)
            {
                continue;
            }

            var key = names.GetString(vri.GetKeyAt(i)).ToUpperInvariant();
            result[key] = new(
                key,
                Streams(entry.GetArray(KnownName.Cert)),
                Streams(entry.GetArray(names.Intern(OcspKey))),
                Streams(entry.GetArray(names.Intern(CrlKey))),
                PdfDate.Parse(entry.GetStringBytes(KnownName.TU)),
                entry.GetStream(names.Intern(TimestampKey))?.DecodeToArray());
        }

        return result;
    }

    /// <summary>Decodes an array of streams.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The decoded streams; entries that are not streams are skipped.</returns>
    private static byte[][] Streams(PdfArray? array)
    {
        if (array is null || array.Count == 0)
        {
            return [];
        }

        var result = new List<byte[]>(Math.Min(array.Count, MaxEntries));
        for (var i = 0; i < array.Count && result.Count < MaxEntries; i++)
        {
            if (array.Get(i).AsStream() is { } stream)
            {
                result.Add(stream.DecodeToArray());
            }
        }

        return [.. result];
    }
}
