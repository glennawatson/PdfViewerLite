// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <content>The mark that redactions were applied, after which only a compact save is safe.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>1 once redactions were applied.</summary>
    private int _requiresCompactSave;

    /// <summary>
    /// Gets a value indicating whether redactions were applied. An incremental update would keep the original file bytes,
    /// and with them the content that was removed, so such a document can only be saved compactly.
    /// </summary>
    public bool RequiresCompactSave => Volatile.Read(ref _requiresCompactSave) != 0;

    /// <summary>Marks the document as redacted.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RequireCompactSave() => Volatile.Write(ref _requiresCompactSave, 1);

    /// <summary>Throws when the document was redacted, before an incremental update is written.</summary>
    /// <exception cref="InvalidOperationException">Redactions were applied.</exception>
    internal void ThrowIfCompactSaveRequired()
    {
        if (RequiresCompactSave)
        {
            throw new InvalidOperationException("Redactions were applied, so the document can only be saved compactly: an incremental update would keep the removed content.");
        }
    }
}
