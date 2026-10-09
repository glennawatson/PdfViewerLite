// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Reads UR3 transform parameters.</summary>
internal static class PdfUsageRightsReader
{
    /// <summary>Gets the /Document key.</summary>
    private static ReadOnlySpan<byte> DocumentKey => "Document"u8;

    /// <summary>Gets the /Form key.</summary>
    private static ReadOnlySpan<byte> FormKey => "Form"u8;

    /// <summary>Gets the /Signature key.</summary>
    private static ReadOnlySpan<byte> SignatureKey => "Signature"u8;

    /// <summary>Gets the /EF key.</summary>
    private static ReadOnlySpan<byte> EmbeddedFilesKey => "EF"u8;

    /// <summary>Gets the /Msg key.</summary>
    private static ReadOnlySpan<byte> MessageKey => "Msg"u8;

    /// <summary>Reads UR3 transform parameters.</summary>
    /// <param name="parameters">The /TransformParams dictionary.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The usage rights.</returns>
    internal static PdfUsageRights Read(PdfDictionary parameters, PdfNameTable names) =>
        new(
            Names(parameters.GetArray(names.Intern(DocumentKey)), names),
            Names(parameters.GetArray(KnownName.Annots), names),
            Names(parameters.GetArray(names.Intern(FormKey)), names),
            Names(parameters.GetArray(names.Intern(SignatureKey)), names),
            Names(parameters.GetArray(names.Intern(EmbeddedFilesKey)), names),
            parameters.GetText(names.Intern(MessageKey)),
            parameters.GetBoolean(KnownName.P));

    /// <summary>Reads an array of names as text.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The names.</returns>
    private static string[] Names(PdfArray? array, PdfNameTable names)
    {
        var result = new string[array?.Count ?? 0];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = names.GetString(array!.GetName(i));
        }

        return result;
    }
}
