// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>XMP metadata.</content>
public sealed partial class PdfDocument
{
    /// <summary>Gets the document's XMP metadata (the catalog's <c>/Metadata</c> stream).</summary>
    /// <returns>The metadata, or <see langword="null"/> when the document has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public XmpMetadata? GetXmp() =>ReadXmp(Catalog);

    /// <summary>Gets a page's XMP metadata.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The metadata, or <see langword="null"/> when the page has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public XmpMetadata? GetXmp(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return ReadXmp(page.Dictionary);
    }

    /// <summary>Reads the XMP metadata of any object (a page, XObject, font, colour space or annotation dictionary, or a stream's dictionary).</summary>
    /// <param name="owner">The dictionary holding a <c>/Metadata</c> entry.</param>
    /// <returns>The metadata, or <see langword="null"/> when there is no readable metadata stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public XmpMetadata? ReadXmp(PdfDictionary owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var packet = owner.GetStream(KnownName.Metadata).DecodeOrEmpty();
        return packet.Length == 0 ? null : XmpParser.Parse(packet);
    }
}
