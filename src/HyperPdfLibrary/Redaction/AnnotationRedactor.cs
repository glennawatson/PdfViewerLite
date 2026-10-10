// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Redaction;

/// <summary>Removes the redact annotations of a page, and the annotations, links and form fields that lie under the redacted areas.</summary>
internal static class AnnotationRedactor
{
    /// <summary>The most levels of parent fields followed.</summary>
    private const int MaxFieldDepth = 32;

    /// <summary>The slack, in points, given to flat annotation rectangles.</summary>
    private const float Slack = 0.25F;

    /// <summary>Applies the annotation mode to a page and removes its redact annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="regions">The areas in user space.</param>
    /// <param name="mode">What to do with annotations the areas touch.</param>
    /// <param name="tally">Receives the counts.</param>
    internal static void Run(PdfDocument document, int pageIndex, PdfRectangle[] regions, PdfRedactionAnnotationMode mode, RedactionTally tally)
    {
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        if (PdfPageAnnotations.GetArray(store, page) is not { } annotations)
        {
            return;
        }

        var drop = new bool[annotations.Count];
        var removedIds = new HashSet<int>();
        for (var i = 0; i < drop.Length; i++)
        {
            drop[i] = ShouldRemove(annotations.GetDictionary(i), regions, mode, out var counts);
            tally.AnnotationsRemoved += drop[i] && counts ? 1 : 0;
            AddId(removedIds, annotations, i, drop[i]);
        }

        RemoveDependents(annotations, drop, removedIds, tally);
        var kept = new PdfArray(store, annotations.Count);
        for (var i = 0; i < drop.Length; i++)
        {
            if (!drop[i])
            {
                kept.Add(annotations.GetRaw(i));
                continue;
            }

            DetachField(store, annotations.GetDictionary(i), annotations.GetRaw(i).AsReference());
        }

        if (kept.Count != annotations.Count)
        {
            _ = PdfPageAnnotations.SetArray(store, page, kept);
        }
    }

    /// <summary>Notes an annotation's object number when it is removed.</summary>
    /// <param name="ids">The numbers.</param>
    /// <param name="annotations">The array.</param>
    /// <param name="index">The index.</param>
    /// <param name="removed">Whether the annotation is removed.</param>
    private static void AddId(HashSet<int> ids, PdfArray annotations, int index, bool removed)
    {
        if (removed && annotations.GetRaw(index).AsReference() is { IsValid: true } id)
        {
            _ = ids.Add(id.Number);
        }
    }

    /// <summary>Decides whether an annotation goes.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="regions">The areas.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="counts">Whether the removal counts in the report; redact annotations themselves do not.</param>
    /// <returns><see langword="true"/> to remove.</returns>
    private static bool ShouldRemove(PdfDictionary? annotation, PdfRectangle[] regions, PdfRedactionAnnotationMode mode, out bool counts)
    {
        counts = true;
        if (annotation is null)
        {
            return false;
        }

        var subtype = annotation.GetName(KnownName.Subtype);
        if (subtype.Is(KnownName.Redact))
        {
            counts = false;
            return true;
        }

        if (mode == PdfRedactionAnnotationMode.Keep || (mode == PdfRedactionAnnotationMode.RemoveLinks && !subtype.Is(KnownName.Link)))
        {
            return false;
        }

        return annotation.TryGetRectangle(KnownName.Rect, out var rect) && Touches(Widen(rect), regions);
    }

    /// <summary>Widens a flat rectangle a little.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <returns>The rectangle, widened where it has no area.</returns>
    private static PdfRectangle Widen(PdfRectangle rect) =>
        new(
        rect.Width > 0 ? rect.Left : rect.Left - Slack,
        rect.Height > 0 ? rect.Bottom : rect.Bottom - Slack,
        rect.Width > 0 ? rect.Right : rect.Right + Slack,
        rect.Height > 0 ? rect.Top : rect.Top + Slack);

    /// <summary>Determines whether any area overlaps a rectangle.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="regions">The areas.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool Touches(PdfRectangle rect, PdfRectangle[] regions)
    {
        foreach (var region in regions)
        {
            if (PageGeometry.Overlaps(rect, region))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Removes pop-ups and replies of annotations that go, and theirs in turn.</summary>
    /// <param name="annotations">The array.</param>
    /// <param name="drop">Which annotations go; updated.</param>
    /// <param name="removedIds">The object numbers of annotations that go; updated.</param>
    /// <param name="tally">Receives the counts.</param>
    private static void RemoveDependents(PdfArray annotations, bool[] drop, HashSet<int> removedIds, RedactionTally tally)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var i = 0; i < drop.Length; i++)
            {
                if (drop[i] || annotations.GetDictionary(i) is not { } annotation || !PointsAtRemoved(annotation, removedIds))
                {
                    continue;
                }

                drop[i] = true;
                changed = true;
                tally.AnnotationsRemoved++;
                AddId(removedIds, annotations, i, true);
            }
        }
    }

    /// <summary>Determines whether an annotation is the pop-up or a reply of a removed annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="removedIds">The object numbers removed.</param>
    /// <returns><see langword="true"/> when its /Parent or /IRT is removed.</returns>
    private static bool PointsAtRemoved(
        PdfDictionary annotation,
        HashSet<int> removedIds) =>
        removedIds.Contains(annotation.GetRaw(KnownName.Parent).AsReference().Number)
        || removedIds.Contains(annotation.GetRaw(KnownName.IRT).AsReference().Number);

    /// <summary>Takes a removed widget's form field out of the form, so the field's value does not stay behind.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="annotation">The removed annotation.</param>
    /// <param name="id">Its object id, or an invalid id for an inline annotation.</param>
    private static void DetachField(PdfObjectStore store, PdfDictionary? annotation, PdfObjectId id)
    {
        if (annotation is null || !id.IsValid || !annotation.IsName(KnownName.Subtype, KnownName.Widget))
        {
            return;
        }

        var current = id;
        for (var depth = 0; depth < MaxFieldDepth; depth++)
        {
            var parentId = StoreReading.GetDictionary(store, current)?.GetRaw(KnownName.Parent).AsReference() ?? default;
            if (!parentId.IsValid)
            {
                RemoveFromFields(store, current);
                return;
            }

            if (!RemoveFromKids(store, parentId, current))
            {
                return;
            }

            current = parentId;
        }
    }

    /// <summary>Removes a child from a field's /Kids.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="parentId">The parent field.</param>
    /// <param name="childId">The child.</param>
    /// <returns><see langword="true"/> when the parent has no children left, so it goes too.</returns>
    private static bool RemoveFromKids(PdfObjectStore store, PdfObjectId parentId, PdfObjectId childId)
    {
        if (StoreReading.GetDictionary(store, parentId) is not { } parent || parent.GetArray(KnownName.Kids) is not { } kids)
        {
            return false;
        }

        var remaining = new PdfArray(store, kids.Count);
        foreach (var item in kids.Items)
        {
            if (item.AsReference() != childId)
            {
                remaining.Add(item);
            }
        }

        var copy = parent.Clone();
        copy.Set(KnownName.Kids, PdfValue.FromArray(remaining));
        StoreEditing.Replace(store, parentId, PdfValue.FromDictionary(copy));
        return remaining.Count == 0;
    }

    /// <summary>Removes a top-level field from the AcroForm's /Fields.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fieldId">The field.</param>
    private static void RemoveFromFields(PdfObjectStore store, PdfObjectId fieldId)
    {
        if (store.Catalog.GetDictionary(KnownName.AcroForm) is not { } form || form.GetArray(KnownName.Fields) is not { } fields)
        {
            return;
        }

        var remaining = new PdfArray(store, fields.Count);
        foreach (var item in fields.Items)
        {
            if (item.AsReference() != fieldId)
            {
                remaining.Add(item);
            }
        }

        var copy = form.Clone();
        copy.Set(KnownName.Fields, PdfValue.FromArray(remaining));
        var raw = store.Catalog.GetRaw(KnownName.AcroForm);
        if (raw.AsReference() is { IsValid: true } formId)
        {
            StoreEditing.Replace(store, formId, PdfValue.FromDictionary(copy));
            return;
        }

        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.AcroForm, PdfValue.FromDictionary(copy));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));
    }
}
