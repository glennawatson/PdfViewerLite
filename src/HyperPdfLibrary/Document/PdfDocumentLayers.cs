// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Layers;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and updates optional content layers.</summary>
public static class PdfDocumentLayers
{
    /// <summary>Gets the document's layers and their visibility.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The document's optional content state.</returns>
    public static PdfOptionalContent GetOptionalContent(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.OptionalContent) is { } existing)
        {
            return existing;
        }

        var created = new PdfOptionalContent(document.Objects);
        created.StartAt(Volatile.Read(ref document.State.LayerVersionFloor));
        _ = Interlocked.CompareExchange(ref document.State.OptionalContent, created, null);
        return Volatile.Read(ref document.State.OptionalContent)!;
    }

    /// <summary>Drops the layer set so it is read again, raising the version floor past every version it handed out.</summary>
    /// <param name="document">The document.</param>
    internal static void DropOptionalContent(PdfDocument document)
    {
        if (Interlocked.Exchange(ref document.State.OptionalContent, null) is not { } dropped)
        {
            return;
        }

        var next = dropped.Version + 1;
        var floor = Volatile.Read(ref document.State.LayerVersionFloor);
        while (next > floor)
        {
            var seen = Interlocked.CompareExchange(ref document.State.LayerVersionFloor, next, floor);
            if (seen == floor)
            {
                return;
            }

            floor = seen;
        }
    }
}
