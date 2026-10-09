// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>A marked-content sequence (<c>BMC</c> or <c>BDC</c>) an object sits inside.</summary>
/// <param name="Tag">The tag, such as <c>/P</c> or <c>/Span</c>.</param>
/// <param name="Properties">The property list, or <see langword="null"/> for <c>BMC</c> and for a list that cannot be read.</param>
/// <param name="PropertiesName">The name of the property list in the resources' /Properties, or none for an inline list.</param>
[DebuggerDisplay("PdfMark: {Tag}")]
public sealed record PdfMark(PdfName Tag, PdfDictionary? Properties, PdfName PropertiesName)
{
    /// <summary>1 once scrubbed.</summary>
    private int _scrubbed;

    /// <summary>Gets the value of <see cref="MarkedContentId"/> when the property list has no /MCID.</summary>
    public static int NoMarkedContentId => -1;

    /// <summary>Gets the /MCID that links the sequence to the structure tree, or <see cref="NoMarkedContentId"/>.</summary>
    public int MarkedContentId => Properties?.GetInt32(KnownName.MCID, NoMarkedContentId) ?? NoMarkedContentId;

    /// <summary>Gets a value indicating whether the sequence's properties carry text that redaction removed from under it (/ActualText, /Alt).</summary>
    public bool IsScrubbed => Volatile.Read(ref _scrubbed) != 0;

    /// <summary>Gets the bytes of the <c>BMC</c> or <c>BDC</c> operator that opens the sequence.</summary>
    internal ByteRange BeginSource { get; init; }

    /// <summary>Makes the regenerated content leave the replacement text (/ActualText and /Alt) out of the sequence's properties.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Scrub() => Volatile.Write(ref _scrubbed, 1);
}
