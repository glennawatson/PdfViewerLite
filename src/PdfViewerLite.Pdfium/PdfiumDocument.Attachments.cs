// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Embedded files.</summary>
public sealed partial class PdfiumDocument : IAttachmentSource
{
    /// <summary>The attachments, read once: viewing never adds or removes them.</summary>
    private DocumentAttachment[]? _attachments;

    /// <inheritdoc/>
    public IReadOnlyList<DocumentAttachment> GetAttachments()
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (_attachments is { } cached)
        {
            return cached;
        }

        var count = IsDisposed ? 0 : Math.Max(0, NativeMethods.FPDFDoc_GetAttachmentCount(_handle));
        var attachments = new List<DocumentAttachment>(count);
        for (var i = 0; i < count; i++)
        {
            var attachment = NativeMethods.FPDFDoc_GetAttachment(_handle, i);
            if (attachment == 0)
            {
                continue;
            }

            attachments.Add(new(i, ReadAttachmentName(attachment, i), GetAttachmentSize(attachment)));
        }

        _attachments = [.. attachments];
        return _attachments;
    }

    /// <inheritdoc/>
    public unsafe bool SaveAttachment(int index, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        byte[]? buffer = null;
        int length;
        using (PdfiumLibrary.EnterScope())
        {
            var attachment = IsDisposed ? 0 : NativeMethods.FPDFDoc_GetAttachment(_handle, index);
            if (attachment == 0 || NativeMethods.FPDFAttachment_GetFileSize(attachment, 0, default, out var size) == 0 || size.Value > int.MaxValue)
            {
                return false;
            }

            length = (int)size.Value;
            buffer = ArrayPool<byte>.Shared.Rent(Math.Max(1, length));
            fixed (byte* pointer = buffer)
            {
                if (NativeMethods.FPDFAttachment_GetFile(attachment, pointer, new((uint)length), out _) == 0)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                    return false;
                }
            }
        }

        // Write outside the PDFium lock so a slow disk never stalls rendering.
        try
        {
            destination.Write(buffer, 0, length);
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Gets an attachment's size.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns>The size in bytes, or 0 when unknown.</returns>
    private static long GetAttachmentSize(nint attachment) =>
        NativeMethods.FPDFAttachment_GetFileSize(attachment, 0, default, out var size) != 0 ? (long)size.Value : 0;

    /// <summary>Reads an attachment's file name.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="index">Its index, used when it has no name.</param>
    /// <returns>The name.</returns>
    private static unsafe string ReadAttachmentName(nint attachment, int index)
    {
        var length = (int)NativeMethods.FPDFAttachment_GetName(attachment, null, default).Value;
        if (length <= sizeof(char))
        {
            return $"Attachment {index + 1}";
        }

        var buffer = new byte[length];
        fixed (byte* pointer = buffer)
        {
            _ = NativeMethods.FPDFAttachment_GetName(attachment, pointer, new((uint)length));
        }

        return NativeText.FromUtf16(buffer);
    }
}
