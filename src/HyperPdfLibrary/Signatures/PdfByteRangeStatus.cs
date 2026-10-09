// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>Whether a signature's /ByteRange is well formed.</summary>
public enum PdfByteRangeStatus
{
    /// <summary>The ranges are in order, inside the file, and skip only the /Contents string.</summary>
    Valid = 0,

    /// <summary>The signature has no /ByteRange.</summary>
    Missing = 1,

    /// <summary>The array does not hold offset and length pairs, or a number is negative.</summary>
    Malformed = 2,

    /// <summary>A range runs past the end of the file.</summary>
    OutOfBounds = 3,

    /// <summary>A range starts before the previous one.</summary>
    OutOfOrder = 4,

    /// <summary>Two ranges overlap.</summary>
    Overlapping = 5,

    /// <summary>A gap between ranges holds something other than the /Contents hex string.</summary>
    GapNotContents = 6,

    /// <summary>The signed bytes or the /Contents string are larger than the limits allow.</summary>
    TooLarge = 7,
}
