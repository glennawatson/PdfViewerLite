// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>Everything a CMap stream defines, collected while it is parsed.</summary>
[DebuggerDisplay("CMapContent: {Cids.Count} CID ranges, {Unicode.Count} Unicode ranges")]
internal sealed class CMapContent
{
    /// <summary>Gets the codespace ranges.</summary>
    internal List<CodespaceRange> Codespaces { get; } = [];

    /// <summary>Gets the CID ranges; each value is the CID of the range's first code.</summary>
    internal CodeRangeMapBuilder Cids { get; } = new();

    /// <summary>Gets the notdef ranges; each value is the CID every code in the range maps to.</summary>
    internal CodeRangeMapBuilder Notdefs { get; } = new();

    /// <summary>Gets the Unicode ranges; each value is where the destination text starts in <see cref="Text"/>.</summary>
    internal CodeRangeMapBuilder Unicode { get; } = new();

    /// <summary>Gets the destination texts, each stored as its length followed by its UTF-16 units.</summary>
    internal List<char> Text { get; } = [];

    /// <summary>Gets or sets the writing mode: 0 for horizontal, 1 for vertical.</summary>
    internal int WritingMode { get; set; }

    /// <summary>Gets or sets the name of the CMap this one uses as its base.</summary>
    internal ReadOnlyMemory<byte> UseCMap { get; set; }

    /// <summary>Gets or sets a value indicating whether the CMap names a base CMap.</summary>
    internal bool HasUseCMap { get; set; }

    /// <summary>Stores a destination text.</summary>
    /// <param name="text">The UTF-16 text.</param>
    /// <returns>Where it starts in <see cref="Text"/>.</returns>
    internal int AddText(ReadOnlySpan<char> text)
    {
        var start = Text.Count;
        Text.Add((char)text.Length);
        foreach (var c in text)
        {
            Text.Add(c);
        }

        return start;
    }
}
