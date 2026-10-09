// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Xfa;

namespace HyperPdfLibrary.Document;

/// <content>XFA form data, kept as raw packets.</content>
public sealed partial class PdfDocument
{
    /// <summary>The packet name given to a single-stream XFA.</summary>
    private const string SingleStreamPacketName = "xdp";

    /// <summary>The number of array elements per packet: a name and a stream.</summary>
    private const int PacketPairLength = 2;

    /// <summary>Gets a value indicating whether the interactive form has XFA data.</summary>
    public bool HasXfa => Catalog.GetDictionary(KnownName.AcroForm)?.Get(KnownName.XFA) is { IsNull: false };

    /// <summary>Gets the XFA packets of the interactive form (<c>/AcroForm /XFA</c>). The packets are returned as decoded bytes and are not interpreted.</summary>
    /// <returns>The packets, or <see langword="null"/> when the form has no XFA data.</returns>
    public PdfXfa? GetXfa()
    {
        var xfa = Catalog.GetDictionary(KnownName.AcroForm)?.Get(KnownName.XFA) ?? default;
        if (xfa.AsStream() is { } single)
        {
            return new([new PdfXfaPacket(SingleStreamPacketName, single.DecodeOrEmpty())], true);
        }

        if (xfa.AsArray() is not { } array)
        {
            return null;
        }

        var packets = new List<PdfXfaPacket>(array.Count / PacketPairLength);
        for (var i = 0; i + 1 < array.Count; i += PacketPairLength)
        {
            if (array.Get(i).Kind == PdfKind.String && array.Get(i + 1).AsStream() is { } stream)
            {
                packets.Add(new(PdfText.Decode(array.Get(i).AsStringBytes()), stream.DecodeOrEmpty()));
            }
        }

        return new(packets, false);
    }
}
