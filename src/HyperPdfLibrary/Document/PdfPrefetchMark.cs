// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Remembers the cache generation at which the objects behind a dictionary were last loaded ahead, so a repeat call can
/// skip the walk while nothing has been dropped from the cache since.
/// </summary>
[DebuggerDisplay("PdfPrefetchMark: generation {_generation}")]
internal sealed class PdfPrefetchMark
{
    /// <summary>The generation the objects were loaded at, or -1 before the first load.</summary>
    private long _generation = -1;

    /// <summary>Determines whether the objects were loaded at a generation.</summary>
    /// <param name="generation">The cache generation now.</param>
    /// <returns><see langword="true"/> when they were loaded at exactly this generation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsLoadedAt(long generation) => Volatile.Read(ref _generation) == generation;

    /// <summary>Records that the objects were loaded at a generation.</summary>
    /// <param name="generation">The generation the load started at.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetLoadedAt(long generation) => Volatile.Write(ref _generation, generation);
}
