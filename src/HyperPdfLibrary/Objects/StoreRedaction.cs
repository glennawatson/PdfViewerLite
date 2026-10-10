// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>Tracks and enforces compact saving after redaction.</summary>
public static class StoreRedaction
{
    /// <summary>
    /// Gets a value indicating whether redactions were applied. An incremental update would keep the original file bytes,
    /// and with them the content that was removed, so such a document can only be saved compactly.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>True when redacted bytes must be removed by a compact save.</returns>
    public static bool RequiresCompactSave(PdfObjectStore self) => Volatile.Read(ref self.RequiresCompactSaveState) != 0;

    /// <summary>Marks the document as redacted.</summary>
    /// <param name = "self">The owned object-store state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RequireCompactSave(PdfObjectStore self) => Volatile.Write(ref self.RequiresCompactSaveState, 1);

    /// <summary>Throws when the document was redacted, before an incremental update is written.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <exception cref = "InvalidOperationException">Redactions were applied.</exception>
    internal static void ThrowIfCompactSaveRequired(PdfObjectStore self)
    {
        if (StoreRedaction.RequiresCompactSave(self))
        {
            throw new InvalidOperationException("Redactions were applied, so the document can only be saved compactly: an incremental update would keep the removed content.");
        }
    }
}
