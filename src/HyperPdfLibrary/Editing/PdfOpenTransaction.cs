// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>The store's link to its open transaction. The caller owns and disposes the transaction, not the store.</summary>
/// <param name="transaction">The open transaction.</param>
[DebuggerDisplay("PdfOpenTransaction: {Transaction.Label}")]
internal sealed class PdfOpenTransaction(PdfEditTransaction transaction)
{
    /// <summary>Gets the open transaction.</summary>
    internal PdfEditTransaction Transaction { get; } = transaction;
}
