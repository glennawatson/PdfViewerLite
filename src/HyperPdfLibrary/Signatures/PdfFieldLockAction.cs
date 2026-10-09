// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>Which fields a FieldMDP lock (/Action) applies to.</summary>
public enum PdfFieldLockAction
{
    /// <summary>The action is missing or not recognised; nothing is locked.</summary>
    None = 0,

    /// <summary>Every field is locked.</summary>
    All = 1,

    /// <summary>Only the listed fields are locked.</summary>
    Include = 2,

    /// <summary>Every field except the listed ones is locked.</summary>
    Exclude = 3,
}
