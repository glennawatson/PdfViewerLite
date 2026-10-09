// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>The changes a certification (DocMDP /P) or field lock allows after signing.</summary>
public enum PdfMdpPermission
{
    /// <summary>No restriction is set.</summary>
    None = 0,

    /// <summary>No changes are allowed (P 1).</summary>
    NoChanges = 1,

    /// <summary>Filling in forms, instantiating page templates and signing are allowed (P 2).</summary>
    FormFillAndSign = 2,

    /// <summary>Form filling, signing and annotation changes are allowed (P 3).</summary>
    AnnotateFormFillAndSign = 3,
}
