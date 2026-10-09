// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Xfa;

/// <summary>The XFA form data of an interactive form (<c>/AcroForm /XFA</c>), kept as raw packets.</summary>
/// <param name="Packets">The packets in file order.</param>
/// <param name="IsSingleStream">Whether <c>/XFA</c> was one stream holding the whole XDP and not an array of named packets.</param>
[DebuggerDisplay("PdfXfa: {Packets.Count} packets")]
public sealed record PdfXfa(IReadOnlyList<PdfXfaPacket> Packets, bool IsSingleStream)
{
    /// <summary>Finds a packet by name.</summary>
    /// <param name="name">The packet name, compared exactly.</param>
    /// <returns>The packet, or null.</returns>
    public PdfXfaPacket? Find(string name)
    {
        foreach (var packet in Packets)
        {
            if (string.Equals(packet.Name, name, StringComparison.Ordinal))
            {
                return packet;
            }
        }

        return null;
    }
}
