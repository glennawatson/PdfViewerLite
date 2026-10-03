// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Comment replies and review status. A reply is a note annotation answering a comment; a review status is a reply
/// carrying <c>/State</c> under the Review state model, as Acrobat writes them. Replies are kept off the page and out
/// of the main list; they are read with the comment they answer.
/// </summary>
internal static unsafe partial class PdfiumAnnotations
{
    /// <summary>Hidden, no zoom and no rotate: replies are listed, not drawn on the page.</summary>
    private const int ReplyFlags = 2 | 8 | 16;

    /// <summary>The review state model's name.</summary>
    private const string ReviewModel = "Review";

    /// <summary>Gets the key of an annotation's unique name.</summary>
    private static ReadOnlySpan<byte> NameKey => "NM"u8;

    /// <summary>Gets the key of the comment a reply answers, as saved files record it.</summary>
    private static ReadOnlySpan<byte> InReplyToKey => "IRT"u8;

    /// <summary>Gets the key under which a reply names its comment until the file is saved.</summary>
    private static ReadOnlySpan<byte> PendingKey => "PVLInReplyTo"u8;

    /// <summary>Gets the key of a review status.</summary>
    private static ReadOnlySpan<byte> StateKey => "State"u8;

    /// <summary>Appends the replies to a comment, in page order (the order they were added).</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The comment's index.</param>
    /// <param name="output">The list receiving the replies.</param>
    internal static void ReadReplies(PdfiumPage page, int index, List<AnnotationReply> output)
    {
        var name = ReadName(page, index);
        var count = NativeMethods.FPDFPage_GetAnnotCount(page.Handle);
        for (var i = 0; i < count; i++)
        {
            var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, i);
            if (annotation == 0)
            {
                continue;
            }

            try
            {
                if (RepliesTo(page, annotation, index, name))
                {
                    output.Add(new(i, ReadString(annotation, ContentsKey), ReadString(annotation, "T"u8), ParseState(ReadString(annotation, StateKey))));
                }
            }
            finally
            {
                NativeMethods.FPDFPage_CloseAnnot(annotation);
            }
        }
    }

    /// <summary>Adds a reply to a comment, or a review status.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The comment's index.</param>
    /// <param name="contents">The reply's text.</param>
    /// <param name="state">The review status, or none for a plain reply.</param>
    /// <returns>The reply's index, or -1.</returns>
    internal static int AddReply(PdfiumPage page, int index, string contents, ReviewState state)
    {
        if (string.IsNullOrWhiteSpace(contents) && state == ReviewState.None)
        {
            return -1;
        }

        var parent = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (parent == 0)
        {
            return -1;
        }

        string name;
        FsRectF rect;
        try
        {
            name = ReadString(parent, NameKey);
            if (name.Length == 0)
            {
                name = NewName();
                _ = SetString(parent, NameKey, name);
            }

            if (NativeMethods.FPDFAnnot_GetRect(parent, out rect) == 0)
            {
                return -1;
            }
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(parent);
        }

        return CreateReply(page, rect, name, contents, state);
    }

    /// <summary>Determines whether an annotation is a reply, so it is listed with its comment rather than on its own.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> for a reply.</returns>
    private static bool IsReply(nint annotation)
    {
        if (ReadString(annotation, PendingKey).Length > 0)
        {
            return true;
        }

        fixed (byte* key = InReplyToKey)
        {
            var linked = NativeMethods.FPDFAnnot_GetLinkedAnnot(annotation, key);
            if (linked == 0)
            {
                return false;
            }

            NativeMethods.FPDFPage_CloseAnnot(linked);
            return true;
        }
    }

    /// <summary>Creates the reply annotation.</summary>
    /// <param name="page">The page.</param>
    /// <param name="rect">The comment's rectangle, which the reply shares.</param>
    /// <param name="parentName">The comment's unique name.</param>
    /// <param name="contents">The reply's text.</param>
    /// <param name="state">The review status.</param>
    /// <returns>The reply's index, or -1.</returns>
    private static int CreateReply(PdfiumPage page, in FsRectF rect, string parentName, string contents, ReviewState state)
    {
        var reply = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeText);
        if (reply == 0)
        {
            return -1;
        }

        try
        {
            _ = NativeMethods.FPDFAnnot_SetRect(reply, rect);
            _ = NativeMethods.FPDFAnnot_SetFlags(reply, ReplyFlags);
            _ = SetString(reply, "T"u8, Author);
            _ = SetModified(reply);
            _ = SetString(reply, NameKey, NewName());
            _ = SetString(reply, PendingKey, parentName);
            if (contents.Length > 0)
            {
                _ = SetString(reply, ContentsKey, contents);
            }

            if (state != ReviewState.None)
            {
                _ = SetString(reply, StateKey, state.ToString());
                _ = SetString(reply, "StateModel"u8, ReviewModel);
            }

            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, reply);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(reply);
        }
    }

    /// <summary>Determines whether an annotation replies to a comment, by reference or, before saving, by name.</summary>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="index">The comment's index.</param>
    /// <param name="name">The comment's unique name, or empty.</param>
    /// <returns><see langword="true"/> when it replies to the comment.</returns>
    private static bool RepliesTo(PdfiumPage page, nint annotation, int index, string name)
    {
        if (name.Length > 0 && string.Equals(ReadString(annotation, PendingKey), name, StringComparison.Ordinal))
        {
            return true;
        }

        fixed (byte* key = InReplyToKey)
        {
            var linked = NativeMethods.FPDFAnnot_GetLinkedAnnot(annotation, key);
            if (linked == 0)
            {
                return false;
            }

            try
            {
                return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, linked) == index;
            }
            finally
            {
                NativeMethods.FPDFPage_CloseAnnot(linked);
            }
        }
    }

    /// <summary>Reads a comment's unique name.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The comment's index.</param>
    /// <returns>The name, or empty.</returns>
    private static string ReadName(PdfiumPage page, int index)
    {
        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return string.Empty;
        }

        try
        {
            return ReadString(annotation, NameKey);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Makes a new unique annotation name.</summary>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string NewName() => string.Create(CultureInfo.InvariantCulture, $"pvl-{Guid.NewGuid():N}");

    /// <summary>Reads a review status.</summary>
    /// <param name="value">The <c>/State</c> value.</param>
    /// <returns>The status; unknown and Unmarked-model values read as none.</returns>
    private static ReviewState ParseState(string value) => value switch
    {
        "Accepted" => ReviewState.Accepted,
        "Rejected" => ReviewState.Rejected,
        "Cancelled" => ReviewState.Cancelled,
        "Completed" => ReviewState.Completed,
        _ => ReviewState.None,
    };
}
