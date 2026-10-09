// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Embedded files and digital signatures.</content>
public sealed partial class PdfDocument
{
    /// <summary>The attachments, read on first use.</summary>
    private PdfAttachment[]? _attachments;

    /// <summary>The signatures, read on first use.</summary>
    private PdfSignatureField[]? _signatures;

    /// <summary>Gets the files embedded in the document, in /EmbeddedFiles name tree order.</summary>
    /// <returns>The attachments.</returns>
    public IReadOnlyList<PdfAttachment> GetAttachments()
    {
        if (Volatile.Read(ref _attachments) is { } cached)
        {
            return cached;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles), entries);
        var attachments = new PdfAttachment[entries.Count];
        for (var i = 0; i < attachments.Length; i++)
        {
            var spec = entries[i].Value.AsDictionary();
            var files = spec?.GetDictionary(KnownName.EF);
            var data = files?.GetStream(KnownName.UF) ?? files?.GetStream(KnownName.F);
            var name = ReadFileSpec(entries[i].Value);
            attachments[i] = new(i, string.IsNullOrEmpty(name) ? string.Create(CultureInfo.InvariantCulture, $"Attachment {i + 1}") : name, data);
        }

        Volatile.Write(ref _attachments, attachments);
        return attachments;
    }

    /// <summary>Gets the document's top-level signature fields, as PDFium lists them.</summary>
    /// <returns>The signatures.</returns>
    public IReadOnlyList<PdfSignatureField> GetSignatures()
    {
        if (Volatile.Read(ref _signatures) is { } cached)
        {
            return cached;
        }

        var fields = Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        var signatures = new List<PdfSignatureField>();
        for (var i = 0; fields is not null && i < fields.Count; i++)
        {
            var field = fields.GetDictionary(i);
            if (field?.IsName(KnownName.FT, KnownName.Sig) == true)
            {
                signatures.Add(ReadSignature(signatures.Count, field.GetDictionary(KnownName.V)));
            }
        }

        var result = signatures.ToArray();
        Volatile.Write(ref _signatures, result);
        return result;
    }

    /// <summary>Reads a signature dictionary.</summary>
    /// <param name="index">The signature's index.</param>
    /// <param name="value">The field's /V signature dictionary, if signed.</param>
    /// <returns>The signature.</returns>
    private PdfSignatureField ReadSignature(int index, PdfDictionary? value)
    {
        if (value is null)
        {
            return new(index, [], [], string.Empty, string.Empty, null);
        }

        var range = value.GetArray(KnownName.ByteRange);
        var byteRange = new long[range?.Count ?? 0];
        for (var i = 0; i < byteRange.Length; i++)
        {
            byteRange[i] = range!.Get(i).AsInteger();
        }

        var subFilter = value.GetName(KnownName.SubFilter);
        return new(
            index,
            value.GetStringBytes(KnownName.Contents).ToArray(),
            byteRange,
            subFilter.IsNone ? string.Empty : Encoding.ASCII.GetString(Objects.Names.GetSpelling(subFilter)),
            value.GetText(KnownName.Reason) ?? string.Empty,
            PdfDate.Parse(value.GetStringBytes(KnownName.M)));
    }
}
