// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>The decoded object stream cache's budget and size.</summary>
public sealed partial class PdfObjectStore
{
    /// <summary>Gets the number of decoded object streams held.</summary>
    internal int CachedObjectStreams => _objectStreams.Count;

    /// <summary>Gets the decoded bytes the cached object streams hold.</summary>
    internal long CachedObjectStreamBytes => _objectStreams.Bytes;

    /// <summary>Changes how many decoded object streams are kept.</summary>
    /// <param name="maxStreams">The most streams to keep; at least one is always kept.</param>
    /// <param name="maxBytes">The most decoded bytes to keep, apart from the newest stream.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetObjectStreamLimits(int maxStreams, long maxBytes) => _objectStreams.SetLimits(maxStreams, maxBytes);
}
