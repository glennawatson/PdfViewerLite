// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A field that sits above carried widgets, copied by <see cref="PdfCarryForms"/>.</summary>
[DebuggerDisplay("PdfFieldNode: {Number}")]
internal sealed class PdfFieldNode
{
    /// <summary>Initializes a new instance of the <see cref="PdfFieldNode"/> class.</summary>
    /// <param name="number">The field's object number in the source.</param>
    /// <param name="source">The field's dictionary in the source.</param>
    /// <param name="parentNumber">The parent field's object number, or 0 for a root field.</param>
    /// <param name="newName">The partial name given to a root field whose own name the target already uses, or <see langword="null"/>.</param>
    internal PdfFieldNode(int number, PdfDictionary source, int parentNumber, byte[]? newName)
    {
        Number = number;
        Source = source;
        ParentNumber = parentNumber;
        NewName = newName;
    }

    /// <summary>Gets the field's object number in the source.</summary>
    internal int Number { get; }

    /// <summary>Gets the field's dictionary in the source.</summary>
    internal PdfDictionary Source { get; }

    /// <summary>Gets the parent field's object number, or 0 for a root field.</summary>
    internal int ParentNumber { get; }

    /// <summary>Gets the partial name given to a root field whose own name the target already uses, or <see langword="null"/>.</summary>
    internal byte[]? NewName { get; }

    /// <summary>Gets the source object numbers of the child fields and widgets that are carried.</summary>
    internal List<int> Kids { get; } = [];
}
