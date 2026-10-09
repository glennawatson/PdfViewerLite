// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Decides whether a change made after signing is allowed by DocMDP (ISO 32000-2 12.8.2.2) and FieldMDP locks. Adding
/// long-term validation data (the /DSS store and document timestamps) is always allowed, as PAdES requires.
/// </summary>
internal static class PdfMdpEvaluator
{
    /// <summary>The single-kind flags, in order.</summary>
    private static readonly PdfModificationKinds[] Kinds =
    [
        PdfModificationKinds.Annotation,
        PdfModificationKinds.FormFill,
        PdfModificationKinds.Signature,
        PdfModificationKinds.SecurityStore,
        PdfModificationKinds.Metadata,
        PdfModificationKinds.Pages,
        PdfModificationKinds.Other,
    ];

    /// <summary>Gets the effective permission: the certification's, narrowed by any lock that sets its own /P.</summary>
    /// <param name="certification">The certification signature's permission, or None.</param>
    /// <param name="locks">The locks in force.</param>
    /// <returns>The permission.</returns>
    internal static PdfMdpPermission Effective(PdfMdpPermission certification, List<PdfFieldLock> locks)
    {
        var effective = certification;
        foreach (var fieldLock in locks)
        {
            if (fieldLock.Permission != PdfMdpPermission.None)
            {
                effective = effective == PdfMdpPermission.None ? fieldLock.Permission : (PdfMdpPermission)Math.Min((int)effective, (int)fieldLock.Permission);
            }
        }

        return effective;
    }

    /// <summary>Decides whether a change is allowed.</summary>
    /// <param name="change">The change.</param>
    /// <param name="permission">The effective permission.</param>
    /// <param name="locks">The locks in force.</param>
    /// <returns><see langword="true"/> when every kind of change it makes is allowed.</returns>
    internal static bool IsPermitted(PdfObjectChange change, PdfMdpPermission permission, List<PdfFieldLock> locks)
    {
        foreach (var kind in Kinds)
        {
            if (((change.Kind & kind) == kind) && !IsPermitted(kind, change.FieldName, permission, locks))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decides whether one kind of change is allowed.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="fieldName">The field it changes, or <see langword="null"/>.</param>
    /// <param name="permission">The effective permission.</param>
    /// <param name="locks">The locks in force.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    private static bool IsPermitted(PdfModificationKinds kind, string? fieldName, PdfMdpPermission permission, List<PdfFieldLock> locks) => kind switch
    {
        PdfModificationKinds.SecurityStore => true,
        PdfModificationKinds.Signature or PdfModificationKinds.Metadata => permission != PdfMdpPermission.NoChanges,
        PdfModificationKinds.FormFill => permission != PdfMdpPermission.NoChanges && !IsLocked(fieldName, locks),
        PdfModificationKinds.Annotation => permission is PdfMdpPermission.None or PdfMdpPermission.AnnotateFormFillAndSign,
        _ => permission == PdfMdpPermission.None,
    };

    /// <summary>Determines whether any lock covers a field.</summary>
    /// <param name="fieldName">The field, or <see langword="null"/>.</param>
    /// <param name="locks">The locks.</param>
    /// <returns><see langword="true"/> when locked.</returns>
    private static bool IsLocked(string? fieldName, List<PdfFieldLock> locks)
    {
        foreach (var fieldLock in locks)
        {
            if (fieldLock.Locks(fieldName))
            {
                return true;
            }
        }

        return false;
    }
}
