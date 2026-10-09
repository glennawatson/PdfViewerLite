// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.HyperPdf;

/// <content>The native annotation editor, made on first use.</content>
public sealed partial class HyperPdfDocument
{
    /// <summary>The native annotation editor, once made.</summary>
    private HyperPdfAnnotations? _annotations;

    /// <summary>Gets the native annotation, text box and image signature editor.</summary>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    internal HyperPdfAnnotations Annotations
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return Volatile.Read(ref _annotations)
                ?? Interlocked.CompareExchange(ref _annotations, new(_document), null)
                ?? Volatile.Read(ref _annotations)!;
        }
    }
}
