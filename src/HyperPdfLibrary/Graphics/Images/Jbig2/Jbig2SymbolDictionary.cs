// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>A decoded symbol dictionary: its exported symbols and, when asked for, its final coding contexts.</summary>
[DebuggerDisplay("Jbig2SymbolDictionary: {Symbols.Count} symbols")]
internal sealed class Jbig2SymbolDictionary : IDisposable
{
    /// <summary>The retained generic contexts, or <see langword="null"/>.</summary>
    private readonly byte[]? _genericContexts;

    /// <summary>The retained refinement contexts, or <see langword="null"/>.</summary>
    private readonly byte[]? _refinementContexts;

    /// <summary>Initializes a new instance of the <see cref="Jbig2SymbolDictionary"/> class.</summary>
    /// <param name="symbols">The exported symbols, now owned by the dictionary.</param>
    /// <param name="genericContexts">The retained generic contexts, or <see langword="null"/>.</param>
    /// <param name="refinementContexts">The retained refinement contexts, or <see langword="null"/>.</param>
    internal Jbig2SymbolDictionary(Jbig2SymbolStore symbols, byte[]? genericContexts, byte[]? refinementContexts)
    {
        Symbols = symbols;
        _genericContexts = genericContexts;
        _refinementContexts = refinementContexts;
    }

    /// <summary>Gets the exported symbols.</summary>
    internal Jbig2SymbolStore Symbols { get; }

    /// <summary>Gets the retained generic contexts; empty when none were kept.</summary>
    internal ReadOnlySpan<byte> GenericContexts => _genericContexts;

    /// <summary>Gets the retained refinement contexts; empty when none were kept.</summary>
    internal ReadOnlySpan<byte> RefinementContexts => _refinementContexts;

    /// <summary>Returns the symbols' buffers to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Symbols.Dispose();
}
