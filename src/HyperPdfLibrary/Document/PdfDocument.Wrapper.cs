// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Unencrypted wrapper documents (ISO 32000-2 7.6.7) that carry an encrypted payload document.</content>
public sealed partial class PdfDocument
{
    /// <summary>Gets a value indicating whether the document is an unencrypted wrapper around an encrypted payload.</summary>
    public bool IsWrapperDocument => GetEncryptedPayload() is not null;

    /// <summary>Gets the /AFRelationship key.</summary>
    private static ReadOnlySpan<byte> RelationshipKey => "AFRelationship"u8;

    /// <summary>Gets the relationship and /Type of an encrypted payload.</summary>
    private static ReadOnlySpan<byte> EncryptedPayloadName => "EncryptedPayload"u8;

    /// <summary>Gets the /EP key of a payload's file specification.</summary>
    private static ReadOnlySpan<byte> PayloadKey => "EP"u8;

    /// <summary>
    /// Gets the encrypted payload of a wrapper document: a document with a /Collection whose associated files (/AF) or
    /// embedded files hold a file specification with <c>/AFRelationship /EncryptedPayload</c> or an /EP dictionary.
    /// </summary>
    /// <returns>The payload, or <see langword="null"/> when this is not a wrapper document.</returns>
    public PdfEncryptedPayload? GetEncryptedPayload()
    {
        if (!Catalog.ContainsKey(KnownName.Collection))
        {
            return null;
        }

        var associated = Catalog.GetArray(KnownName.AF);
        for (var i = 0; associated is not null && i < associated.Count; i++)
        {
            if (ReadPayload(associated.Get(i)) is { } payload)
            {
                return payload;
            }
        }

        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles), entries);
        foreach (var entry in entries)
        {
            if (ReadPayload(entry.Value) is { } payload)
            {
                return payload;
            }
        }

        return null;
    }

    /// <summary>Opens the encrypted payload of a wrapper document.</summary>
    /// <param name="password">The payload's password, or <see langword="null"/>.</param>
    /// <returns>The payload document; the caller disposes it.</returns>
    /// <exception cref="PdfException">This is not a wrapper document, or the payload cannot be opened.</exception>
    public PdfDocument OpenEncryptedPayload(string? password)
    {
        var payload = GetEncryptedPayload() ?? throw new PdfException(PdfError.Format, "The document is not a wrapper document.");
        return Open(payload.GetBytes(), password);
    }

    /// <summary>Determines whether an /AFRelationship is EncryptedPayload.</summary>
    /// <param name="relationship">The relationship.</param>
    /// <param name="names">The name table.</param>
    /// <returns><see langword="true"/> for an encrypted payload.</returns>
    private static bool IsPayloadRelationship(PdfName relationship, PdfNameTable names) =>
        !relationship.IsNone && names.NameEquals(relationship, EncryptedPayloadName);

    /// <summary>Creates the payload record from its file specification and /EP dictionary.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="spec">The file specification.</param>
    /// <param name="details">The /EP dictionary, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The payload.</returns>
    private static PdfEncryptedPayload CreatePayload(string fileName, PdfDictionary spec, PdfDictionary? details, PdfNameTable names)
    {
        var files = spec.GetDictionary(KnownName.EF);
        var filter = details?.GetName(KnownName.Subtype) ?? default;
        return new(
            fileName,
            filter.IsNone ? string.Empty : names.GetString(filter),
            details?.GetText(KnownName.Version),
            spec.GetText(KnownName.Desc),
            files?.GetStream(KnownName.UF) ?? files?.GetStream(KnownName.F));
    }

    /// <summary>Reads a file specification as an encrypted payload.</summary>
    /// <param name="value">The file specification.</param>
    /// <returns>The payload, or <see langword="null"/> when the specification is not one.</returns>
    private PdfEncryptedPayload? ReadPayload(PdfValue value)
    {
        if (value.AsDictionary() is not { } spec)
        {
            return null;
        }

        var names = Objects.Names;
        var details = spec.GetDictionary(names.Intern(PayloadKey));
        return details is null && !IsPayloadRelationship(spec.GetName(names.Intern(RelationshipKey)), names)
            ? null
            : CreatePayload(ReadFileSpec(value) ?? string.Empty, spec, details, names);
    }
}
