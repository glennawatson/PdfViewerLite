// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The structure names that are not in <see cref="KnownName"/>, interned once in a document's name table.</summary>
[DebuggerDisplay("TaggedNames")]
internal sealed class TaggedNames
{
    /// <summary>Initializes a new instance of the <see cref="TaggedNames"/> class.</summary>
    /// <param name="names">The document's name table.</param>
    internal TaggedNames(PdfNameTable names)
    {
        Marked = names.Intern("Marked"u8);
        Stm = names.Intern("Stm"u8);
        NS = names.Intern("NS"u8);
        Namespaces = names.Intern("Namespaces"u8);
        RoleMapNS = names.Intern("RoleMapNS"u8);
        IDTree = names.Intern("IDTree"u8);
        Placement = names.Intern("Placement"u8);
        Scope = names.Intern("Scope"u8);
        Headers = names.Intern("Headers"u8);
        RowSpan = names.Intern("RowSpan"u8);
        ColSpan = names.Intern("ColSpan"u8);
        ListNumbering = names.Intern("ListNumbering"u8);
        Summary = names.Intern("Summary"u8);
        Row = names.Intern("Row"u8);
        Column = names.Intern("Column"u8);
        Both = names.Intern("Both"u8);
    }

    /// <summary>Gets <c>/Marked</c>.</summary>
    internal PdfName Marked { get; }

    /// <summary>Gets <c>/Stm</c>.</summary>
    internal PdfName Stm { get; }

    /// <summary>Gets <c>/NS</c>.</summary>
    internal PdfName NS { get; }

    /// <summary>Gets <c>/Namespaces</c>.</summary>
    internal PdfName Namespaces { get; }

    /// <summary>Gets <c>/RoleMapNS</c>.</summary>
    internal PdfName RoleMapNS { get; }

    /// <summary>Gets <c>/IDTree</c>.</summary>
    internal PdfName IDTree { get; }

    /// <summary>Gets <c>/Placement</c>.</summary>
    internal PdfName Placement { get; }

    /// <summary>Gets <c>/Scope</c>.</summary>
    internal PdfName Scope { get; }

    /// <summary>Gets <c>/Headers</c>.</summary>
    internal PdfName Headers { get; }

    /// <summary>Gets <c>/RowSpan</c>.</summary>
    internal PdfName RowSpan { get; }

    /// <summary>Gets <c>/ColSpan</c>.</summary>
    internal PdfName ColSpan { get; }

    /// <summary>Gets <c>/ListNumbering</c>.</summary>
    internal PdfName ListNumbering { get; }

    /// <summary>Gets <c>/Summary</c>.</summary>
    internal PdfName Summary { get; }

    /// <summary>Gets the <c>/Row</c> scope value.</summary>
    internal PdfName Row { get; }

    /// <summary>Gets the <c>/Column</c> scope value.</summary>
    internal PdfName Column { get; }

    /// <summary>Gets the <c>/Both</c> scope value.</summary>
    internal PdfName Both { get; }
}
