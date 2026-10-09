// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Comment replies and review status. A reply is a hidden note answering a comment through a standard <c>/IRT</c>
/// reference and <c>/RT /R</c>; a review status is a reply carrying <c>/State</c> under the Review state model.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>Hidden, no zoom and no rotate: replies are listed, not drawn on the page.</summary>
    private const PdfAnnotationFlags ReplyFlags = PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoZoom | PdfAnnotationFlags.NoRotate;

    /// <inheritdoc/>
    public void GetReplies(int pageIndex, int index, List<AnnotationReply> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        lock (_gate)
        {
            if (GetPage(pageIndex) is not { } page || PdfPageAnnotations.GetArray(_store, page) is not { } annotations)
            {
                return;
            }

            var parent = annotations.GetDictionary(index);
            var parentId = annotations.GetRaw(index).AsReference();
            for (var i = 0; i < annotations.Count; i++)
            {
                if (parent is not null && annotations.GetDictionary(i) is { } reply && !IsRemoved(reply) && RepliesTo(reply, parent, parentId))
                {
                    output.Add(new(i, PdfAnnotations.GetText(reply, KnownName.Contents), PdfAnnotations.GetText(reply, KnownName.T), ParseState(reply)));
                }
            }
        }
    }

    /// <inheritdoc/>
    public int AddReply(int pageIndex, int index, string contents, ReviewState state)
    {
        ArgumentNullException.ThrowIfNull(contents);
        if (string.IsNullOrWhiteSpace(contents) && state == ReviewState.None)
        {
            return -1;
        }

        lock (_gate)
        {
            if (GetPage(pageIndex) is not { } page || PdfPageAnnotations.Get(_store, page, index) is not { } parent)
            {
                return -1;
            }

            var parentId = EnsureNamed(pageIndex, page, index, parent);
            if (!parentId.IsValid)
            {
                return -1;
            }

            var reply = PdfAnnotations.Create(_store, KnownName.Text, PdfAnnotations.GetRectangle(parent));
            PdfAnnotations.SetFlags(reply, ReplyFlags);
            PdfAnnotations.SetText(reply, KnownName.T, Author);
            SetModified(reply);
            PdfAnnotations.SetText(reply, KnownName.NM, NewName());
            PdfAnnotations.SetInReplyTo(reply, parentId);
            if (contents.Length > 0)
            {
                PdfAnnotations.SetText(reply, KnownName.Contents, contents);
            }

            var name = StateName(state);
            if (!name.IsEmpty)
            {
                PdfAnnotations.SetReviewState(reply, name);
            }

            return Changed(pageIndex, PdfPageAnnotations.Append(_store, page, reply));
        }
    }

    /// <summary>Makes a new unique annotation name.</summary>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string NewName() => string.Create(CultureInfo.InvariantCulture, $"pvl-{Guid.NewGuid():N}");

    /// <summary>Gets a review status's name.</summary>
    /// <param name="state">The status.</param>
    /// <returns>The name, or empty for none.</returns>
    private static ReadOnlySpan<byte> StateName(ReviewState state) => state switch
    {
        ReviewState.Accepted => "Accepted"u8,
        ReviewState.Rejected => "Rejected"u8,
        ReviewState.Cancelled => "Cancelled"u8,
        ReviewState.Completed => "Completed"u8,
        _ => [],
    };

    /// <summary>Reads a review status; unknown and Unmarked-model values read as none.</summary>
    /// <param name="reply">The reply.</param>
    /// <returns>The status.</returns>
    private static ReviewState ParseState(PdfDictionary reply)
    {
        if (PdfAnnotations.TextEquals(reply, KnownName.State, "Accepted"u8))
        {
            return ReviewState.Accepted;
        }

        if (PdfAnnotations.TextEquals(reply, KnownName.State, "Rejected"u8))
        {
            return ReviewState.Rejected;
        }

        if (PdfAnnotations.TextEquals(reply, KnownName.State, "Cancelled"u8))
        {
            return ReviewState.Cancelled;
        }

        return PdfAnnotations.TextEquals(reply, KnownName.State, "Completed"u8) ? ReviewState.Completed : ReviewState.None;
    }

    /// <summary>Determines whether an annotation replies to a comment: by reference, or by name before the PDFium engine saved it.</summary>
    /// <param name="reply">The annotation.</param>
    /// <param name="parent">The comment.</param>
    /// <param name="parentId">The comment's object id, or an invalid id for an inline comment.</param>
    /// <returns><see langword="true"/> when it replies to the comment.</returns>
    private bool RepliesTo(PdfDictionary reply, PdfDictionary parent, PdfObjectId parentId)
    {
        var target = reply.GetRaw(KnownName.IRT);
        if (target.IsReference && parentId.IsValid)
        {
            return target.AsReference().Number == parentId.Number && PdfAnnotations.GetInReplyTo(reply) is not null;
        }

        if (ReferenceEquals(PdfAnnotations.GetInReplyTo(reply), parent))
        {
            return true;
        }

        var pending = reply.GetStringBytes(_names.PendingReply);
        return !pending.IsEmpty && pending.SequenceEqual(parent.GetStringBytes(KnownName.NM));
    }

    /// <summary>Gives a comment a unique name when it has none, and makes it an object of its own so replies can refer to it.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The comment's index.</param>
    /// <param name="parent">The comment.</param>
    /// <returns>The comment's object id, or an invalid id when the page cannot be changed.</returns>
    private PdfObjectId EnsureNamed(int pageIndex, PdfPage page, int index, PdfDictionary parent)
    {
        if (!PdfAnnotations.HasText(parent, KnownName.NM))
        {
            var named = parent.Clone();
            PdfAnnotations.SetText(named, KnownName.NM, NewName());
            if (!Commit(pageIndex, page, index, named))
            {
                return default;
            }
        }

        return PdfPageAnnotations.MakeIndirect(_store, page, index);
    }
}
