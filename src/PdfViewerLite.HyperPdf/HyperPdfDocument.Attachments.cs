// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Signatures;

namespace PdfViewerLite.HyperPdf;

/// <content>Embedded files and digital signatures, read by the managed library.</content>
public sealed partial class HyperPdfDocument : IAttachmentSource, ISignatureSource
{
    /// <summary>The largest encoded embedded file decoded just to report its size, in bytes.</summary>
    private const int MaxListingDecode = 64 * 1024 * 1024;

    /// <summary>The attachments, read once; viewing never adds or removes them.</summary>
    private DocumentAttachment[]? _attachments;

    /// <summary>The signatures, read once; they cannot change while the document is open.</summary>
    private RawSignature[]? _signatures;

    /// <inheritdoc/>
    public int SignatureCount => IsDisposed ? 0 : _document.GetSignatures().Count;

    /// <inheritdoc/>
    public IReadOnlyList<DocumentAttachment> GetAttachments()
    {
        if (IsDisposed)
        {
            return [];
        }

        if (Volatile.Read(ref _attachments) is { } cached)
        {
            return cached;
        }

        var source = _document.GetAttachments();
        var attachments = new DocumentAttachment[source.Count];
        for (var i = 0; i < attachments.Length; i++)
        {
            attachments[i] = new(source[i].Index, source[i].Name, AttachmentSize(source[i].Data));
        }

        Volatile.Write(ref _attachments, attachments);
        return attachments;
    }

    /// <inheritdoc/>
    public bool SaveAttachment(int index, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var attachments = IsDisposed ? [] : _document.GetAttachments();
        if ((uint)index >= (uint)attachments.Count || attachments[index].Data is not { } data)
        {
            return false;
        }

        var output = default(PooledBuffer);
        try
        {
            _ = data.Decode(ref output);
            destination.Write(output.WrittenSpan);
            return true;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<RawSignature> GetSignatures()
    {
        if (IsDisposed)
        {
            return [];
        }

        if (Volatile.Read(ref _signatures) is { } cached)
        {
            return cached;
        }

        var source = _document.GetSignatures();
        var signatures = new RawSignature[source.Count];
        for (var i = 0; i < signatures.Length; i++)
        {
            var field = source[i];
            signatures[i] = new(field.Index, field.Contents, field.ByteRange, field.SubFilter, field.Reason, field.SigningTime);
        }

        Volatile.Write(ref _signatures, signatures);
        return signatures;
    }

    /// <summary>
    /// Gets an embedded file's size: the /Params /Size the file records, else the decoded length. A stream too big to
    /// decode for a listing, or one that fails to decode, reports 0 so one bad file never breaks the list.
    /// </summary>
    /// <param name="data">The embedded file stream.</param>
    /// <returns>The size in bytes, or 0 when there is no data or it cannot be read.</returns>
    private static long AttachmentSize(PdfStream? data)
    {
        if (data is null)
        {
            return 0;
        }

        var recorded = data.Dictionary.GetDictionary(KnownName.Params)?.GetInteger(KnownName.Size, -1) ?? -1;
        if (recorded >= 0)
        {
            return recorded;
        }

        return data.RawLength > MaxListingDecode ? 0 : DecodedLength(data);
    }

    /// <summary>Decodes an embedded file to measure it.</summary>
    /// <param name="data">The embedded file stream.</param>
    /// <returns>The decoded length, or 0 when decoding fails.</returns>
    private static long DecodedLength(PdfStream data)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = data.Decode(ref output);
            return output.Length;
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return 0;
        }
        finally
        {
            output.Dispose();
        }
    }
}
