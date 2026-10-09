// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>The names the optimiser uses that are not in <see cref="KnownName"/>, interned in one document's table.</summary>
[DebuggerDisplay("OptimizerNames")]
internal sealed class OptimizerNames
{
    /// <summary>Initializes a new instance of the <see cref="OptimizerNames"/> class.</summary>
    /// <param name="names">The document's name table.</param>
    internal OptimizerNames(PdfNameTable names)
    {
        Matte = names.Intern("Matte"u8);
        Thumb = names.Intern("Thumb"u8);
        PieceInfo = names.Intern("PieceInfo"u8);
        Marked = names.Intern("Marked"u8);
        DisplayDocTitle = names.Intern("DisplayDocTitle"u8);
        ParentTreeNextKey = names.Intern("ParentTreeNextKey"u8);
        Length1 = names.Intern("Length1"u8);
        OutputIntents = names.Intern("OutputIntents"u8);
        Figure = names.Intern("Figure"u8);
        Sect = names.Intern("Sect"u8);
        Document = names.Intern("Document"u8);
        Span = names.Intern("Span"u8);
        ListItem = names.Intern("LI"u8);
        ListBody = names.Intern("LBody"u8);
        Headings =
        [
            names.Intern("H1"u8),
            names.Intern("H2"u8),
            names.Intern("H3"u8),
            names.Intern("H4"u8),
            names.Intern("H5"u8),
            names.Intern("H6"u8),
        ];
    }

    /// <summary>Gets <c>/Matte</c>.</summary>
    internal PdfName Matte { get; }

    /// <summary>Gets <c>/Thumb</c>.</summary>
    internal PdfName Thumb { get; }

    /// <summary>Gets <c>/PieceInfo</c>.</summary>
    internal PdfName PieceInfo { get; }

    /// <summary>Gets <c>/Marked</c>.</summary>
    internal PdfName Marked { get; }

    /// <summary>Gets <c>/DisplayDocTitle</c>.</summary>
    internal PdfName DisplayDocTitle { get; }

    /// <summary>Gets <c>/ParentTreeNextKey</c>.</summary>
    internal PdfName ParentTreeNextKey { get; }

    /// <summary>Gets <c>/Length1</c>.</summary>
    internal PdfName Length1 { get; }

    /// <summary>Gets <c>/OutputIntents</c>.</summary>
    internal PdfName OutputIntents { get; }

    /// <summary>Gets <c>/Figure</c>.</summary>
    internal PdfName Figure { get; }

    /// <summary>Gets <c>/Sect</c>.</summary>
    internal PdfName Sect { get; }

    /// <summary>Gets <c>/Document</c>.</summary>
    internal PdfName Document { get; }

    /// <summary>Gets <c>/Span</c>.</summary>
    internal PdfName Span { get; }

    /// <summary>Gets <c>/LI</c>.</summary>
    internal PdfName ListItem { get; }

    /// <summary>Gets <c>/LBody</c>.</summary>
    internal PdfName ListBody { get; }

    /// <summary>Gets <c>/H1</c> to <c>/H6</c>.</summary>
    internal PdfName[] Headings { get; }
}
