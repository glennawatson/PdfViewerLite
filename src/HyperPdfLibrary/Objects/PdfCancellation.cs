// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// The cancellation token of the synchronous core call running on this thread. An async operation enters a scope around
/// its synchronous section, so loops deep in the parser, the filters, the content interpreter and the image decoders stop
/// on the caller's token without passing it through every signature. A scope never spans an <see langword="await"/>.
/// </summary>
internal static class PdfCancellation
{
    /// <summary>The token of the running call; the default token never cancels.</summary>
    [ThreadStatic]
    private static CancellationToken _ambient;

    /// <summary>Makes a token the running call's token until the scope is disposed.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The scope, which restores the previous token.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Scope Enter(CancellationToken token)
    {
        var previous = _ambient;
        _ambient = token;
        return new(previous);
    }

    /// <summary>Throws when the running call's token is cancelled.</summary>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ThrowIfCancelled() => _ambient.ThrowIfCancellationRequested();

    /// <summary>Restores the previous token when disposed.</summary>
    internal readonly ref struct Scope
    {
        /// <summary>The token to restore.</summary>
        private readonly CancellationToken _previous;

        /// <summary>Initializes a new instance of the <see cref="Scope"/> struct.</summary>
        /// <param name="previous">The token to restore.</param>
        internal Scope(CancellationToken previous) => _previous = previous;

        /// <summary>Restores the previous token.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Dispose() => _ambient = _previous;
    }
}
