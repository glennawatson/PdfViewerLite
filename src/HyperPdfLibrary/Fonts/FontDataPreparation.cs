// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
namespace HyperPdfLibrary.Fonts;

/// <summary>Records preparation of one page snapshot.</summary>
internal sealed class FontDataPreparation
{
    /// <summary>Whether every asset has been prepared.</summary>
    private bool _ready;

    /// <summary>Gets the gate guarding the pending task.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>Gets or sets the current preparation attempt under the gate.</summary>
    internal Task? Pending { get; set; }

    /// <summary>Gets or sets the token that owns the current attempt under the gate.</summary>
    internal CancellationToken PendingCancellation { get; set; }

    /// <summary>Gets whether every asset has been prepared.</summary>
    internal bool Ready => Volatile.Read(ref _ready);

    /// <summary>Publishes successful preparation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkReady() => Volatile.Write(ref _ready, true);
}
