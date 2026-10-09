// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>A FieldMDP lock: a signature field's /Lock, or a FieldMDP transform in a signature's /Reference.</summary>
/// <param name="Action">Which fields the lock applies to.</param>
/// <param name="Fields">The fully qualified field names that /Action refers to.</param>
/// <param name="Permission">The PDF 2.0 /P of a /Lock dictionary, which also restricts the whole document; None when absent.</param>
[DebuggerDisplay("PdfFieldLock: {Action} {Fields.Length} fields")]
public sealed record PdfFieldLock(PdfFieldLockAction Action, string[] Fields, PdfMdpPermission Permission)
{
    /// <summary>Determines whether the lock covers a field.</summary>
    /// <param name="fieldName">The field's fully qualified name, or <see langword="null"/> for a change to no particular field.</param>
    /// <returns><see langword="true"/> when changes to the field are locked.</returns>
    public bool Locks(string? fieldName) => fieldName is not null && Action switch
    {
        PdfFieldLockAction.All => true,
        PdfFieldLockAction.Include => Lists(fieldName),
        PdfFieldLockAction.Exclude => !Lists(fieldName),
        _ => false,
    };

    /// <summary>Determines whether the field or one of its parents is listed.</summary>
    /// <param name="fieldName">The field's fully qualified name.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private bool Lists(string fieldName)
    {
        foreach (var name in Fields)
        {
            if (string.Equals(name, fieldName, StringComparison.Ordinal)
                || (fieldName.Length > name.Length && fieldName[name.Length] == '.' && fieldName.StartsWith(name, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
