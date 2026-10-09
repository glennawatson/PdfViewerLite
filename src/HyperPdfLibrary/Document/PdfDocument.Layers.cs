// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Layers;

namespace HyperPdfLibrary.Document;

/// <content>Layers.</content>
public sealed partial class PdfDocument
{
    /// <summary>The layers, read on first use.</summary>
    private PdfOptionalContent? _optionalContent;

    /// <summary>
    /// The lowest version a newly read layer set starts at. Renderers key their cached pages on the layer version, so a
    /// layer set read again after an edit must start above every version an earlier set handed out.
    /// </summary>
    private int _layerVersionFloor;

    /// <summary>Gets the document's layers and their visibility.</summary>
    public PdfOptionalContent OptionalContent
    {
        get
        {
            if (Volatile.Read(ref _optionalContent) is { } existing)
            {
                return existing;
            }

            var created = new PdfOptionalContent(Objects);
            created.StartAt(Volatile.Read(ref _layerVersionFloor));
            _ = Interlocked.CompareExchange(ref _optionalContent, created, null);
            return Volatile.Read(ref _optionalContent)!;
        }
    }

    /// <summary>Drops the layer set so it is read again, raising the version floor past every version it handed out.</summary>
    private void DropOptionalContent()
    {
        if (Interlocked.Exchange(ref _optionalContent, null) is not { } dropped)
        {
            return;
        }

        var next = dropped.Version + 1;
        var floor = Volatile.Read(ref _layerVersionFloor);
        while (next > floor)
        {
            var seen = Interlocked.CompareExchange(ref _layerVersionFloor, next, floor);
            if (seen == floor)
            {
                return;
            }

            floor = seen;
        }
    }
}
