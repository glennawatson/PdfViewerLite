// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The JPEG 2000 codestream marker codes (ISO 15444-1 annex A) the decoder reads.</summary>
internal static class JpxMarkers
{
    /// <summary>Start of codestream.</summary>
    internal const int StartOfCodestream = 0xFF4F;

    /// <summary>Image and tile size.</summary>
    internal const int ImageSize = 0xFF51;

    /// <summary>Extended capabilities (ISO 15444-1 A.5.2), which declare Part 15 high-throughput coding.</summary>
    internal const int Capabilities = 0xFF50;

    /// <summary>Coding style default.</summary>
    internal const int CodingStyle = 0xFF52;

    /// <summary>Coding style component.</summary>
    internal const int ComponentCodingStyle = 0xFF53;

    /// <summary>Quantization default.</summary>
    internal const int Quantization = 0xFF5C;

    /// <summary>Quantization component.</summary>
    internal const int ComponentQuantization = 0xFF5D;

    /// <summary>Region of interest.</summary>
    internal const int RegionOfInterest = 0xFF5E;

    /// <summary>Progression order change.</summary>
    internal const int ProgressionChange = 0xFF5F;

    /// <summary>Packed packet headers in the main header.</summary>
    internal const int PackedMainHeaders = 0xFF60;

    /// <summary>Packed packet headers in a tile-part header.</summary>
    internal const int PackedTileHeaders = 0xFF61;

    /// <summary>Start of tile-part.</summary>
    internal const int StartOfTile = 0xFF90;

    /// <summary>Start of packet.</summary>
    internal const int StartOfPacket = 0xFF91;

    /// <summary>End of packet header.</summary>
    internal const int EndOfPacketHeader = 0xFF92;

    /// <summary>Start of data.</summary>
    internal const int StartOfData = 0xFF93;

    /// <summary>End of codestream.</summary>
    internal const int EndOfCodestream = 0xFFD9;

    /// <summary>The bytes of a marker code.</summary>
    internal const int MarkerBytes = 2;

    /// <summary>The bytes of a start-of-packet marker segment, marker included.</summary>
    internal const int StartOfPacketBytes = 6;
}
