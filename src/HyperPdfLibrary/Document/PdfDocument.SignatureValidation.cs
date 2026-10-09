// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Document;

/// <content>Signature validation, modification detection and the document security store.</content>
public sealed partial class PdfDocument
{
    /// <summary>The signature details, read on first use.</summary>
    private PdfSignatureDetails[]? _signatureDetails;

    /// <summary>The document security store, read on first use.</summary>
    private PdfSecurityStore? _securityStore;

    /// <summary>The revisions, read on first use.</summary>
    private PdfRevision[]? _revisions;

    /// <summary>Gets each signature field's modification-detection data: DocMDP, FieldMDP, locks and usage rights.</summary>
    /// <returns>The details, one per entry of <see cref="GetSignatures"/>.</returns>
    public IReadOnlyList<PdfSignatureDetails> GetSignatureDetails()
    {
        if (Volatile.Read(ref _signatureDetails) is { } cached)
        {
            return cached;
        }

        var details = PdfSignatureDetailsReader.Read(this);
        Volatile.Write(ref _signatureDetails, details);
        return details;
    }

    /// <summary>Gets the document security store (/DSS) and its /VRI entries.</summary>
    /// <returns>The store, or <see cref="PdfSecurityStore.Empty"/>.</returns>
    public PdfSecurityStore GetSecurityStore()
    {
        if (Volatile.Read(ref _securityStore) is { } cached)
        {
            return cached;
        }

        var store = PdfSecurityStoreReader.Read(Catalog, Objects.Names);
        Volatile.Write(ref _securityStore, store);
        return store;
    }

    /// <summary>Gets the file's revisions: the original save and each incremental update, oldest first.</summary>
    /// <returns>The revisions.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<PdfRevision> GetRevisions() => GetRevisionArray();

    /// <summary>Gets the usage rights of the document's UR3 signature (/Perms /UR3).</summary>
    /// <returns>The rights, or <see langword="null"/> when there is no UR3 signature.</returns>
    public PdfUsageRights? GetUsageRights()
    {
        var signature = Catalog.GetDictionary(KnownName.Perms)?.GetDictionary(Objects.Names.Intern("UR3"u8));
        return PdfSignatureDictionaries.ReadReferences(signature?.GetArray(KnownName.Reference), Objects.Names).UsageRights;
    }

    /// <summary>Checks every signature without the network, trusting the system's roots.</summary>
    /// <returns>One report per entry of <see cref="GetSignatures"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<PdfSignatureValidationReport> ValidateSignatures() => ValidateSignatures(PdfSignatureValidationOptions.Default);

    /// <summary>Checks every signature: byte ranges, changes after signing and whether MDP allows them, digests, signatures, timestamps and chains.</summary>
    /// <param name="options">How to check.</param>
    /// <returns>One report per entry of <see cref="GetSignatures"/>.</returns>
    public IReadOnlyList<PdfSignatureValidationReport> ValidateSignatures(PdfSignatureValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return PdfSignatureValidator.Validate(this, options);
    }

    /// <summary>Gets the revisions, reading them on first use.</summary>
    /// <returns>The revisions.</returns>
    internal PdfRevision[] GetRevisionArray()
    {
        if (Volatile.Read(ref _revisions) is { } cached)
        {
            return cached;
        }

        var revisions = Objects.ReadRevisions();
        Volatile.Write(ref _revisions, revisions);
        return revisions;
    }
}
