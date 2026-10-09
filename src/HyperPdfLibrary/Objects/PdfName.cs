// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// An interned PDF name. Known names have the same id in every document; other names are numbered by the document's
/// <see cref="PdfNameTable"/>, so comparing two names is one integer comparison.
/// </summary>
/// <param name="Id">The interned id.</param>
[DebuggerDisplay("PdfName: {Id}")]
public readonly record struct PdfName(int Id)
{
    /// <summary>Gets a value indicating whether this is one of the <see cref="KnownName"/> values.</summary>
    public bool IsKnown => (uint)Id < KnownNameSpellings.Count;

    /// <summary>Gets a value indicating whether the name is empty (no name).</summary>
    public bool IsNone => Id == 0;

    /// <summary>Converts a known name.</summary>
    /// <param name="name">The known name.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator PdfName(KnownName name) => new((int)name);

    /// <summary>Converts a known name.</summary>
    /// <param name="name">The known name.</param>
    /// <returns>The name.</returns>
    public static PdfName FromKnownName(KnownName name) => new((int)name);

    /// <summary>Determines whether this is a known name.</summary>
    /// <param name="name">The known name.</param>
    /// <returns><see langword="true"/> when they are the same.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Is(KnownName name) => Id == (int)name;

    /// <summary>Gets the known name, or <see cref="KnownName.None"/> for a document-specific name.</summary>
    /// <returns>The known name.</returns>
    public KnownName ToKnownName() => IsKnown ? (KnownName)Id : KnownName.None;
}
