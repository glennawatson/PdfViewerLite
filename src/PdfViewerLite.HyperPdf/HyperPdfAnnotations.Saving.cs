// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Saving. A signed document is saved incrementally so its signatures stay valid; others are rewritten compactly.
/// Annotations removed but kept are left out of the saved file and stay in memory, so removing them can still be undone.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>How deep the form field tree is searched for signatures.</summary>
    private const int MaxFieldDepth = 32;

    /// <inheritdoc/>
    public bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (_gate)
        {
            if (_document.IsDisposed)
            {
                return false;
            }

            var hidden = HideRemoved();
            try
            {
                if (HasSignedFields(_store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields), 0))
                {
                    PdfIncrementalWriter.Save(_store, destination);
                }
                else
                {
                    PdfCompactWriter.Save(_store, PdfCompactOptions.Default, destination);
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (PdfException)
            {
                return false;
            }
            finally
            {
                Restore(hidden);
            }

            Volatile.Write(ref _unsavedChanges, 0);
            return true;
        }
    }

    /// <summary>Determines whether a field list holds a signed signature field: <c>/FT /Sig</c> with a <c>/V</c>.</summary>
    /// <param name="fields">The fields, or their kids.</param>
    /// <param name="depth">How deep in the field tree the list is.</param>
    /// <returns><see langword="true"/> when one is signed.</returns>
    private static bool HasSignedFields(PdfArray? fields, int depth)
    {
        if (fields is null || depth > MaxFieldDepth)
        {
            return false;
        }

        for (var i = 0; i < fields.Count; i++)
        {
            if (fields.GetDictionary(i) is not { } field)
            {
                continue;
            }

            if ((field.IsName(KnownName.FT, KnownName.Sig) && !field.Get(KnownName.V).IsNull) || HasSignedFields(field.GetArray(KnownName.Kids), depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Takes annotations removed but kept off their pages for saving.</summary>
    /// <returns>The pages changed and their full annotation arrays, to put back.</returns>
    private List<RemovedPage> HideRemoved()
    {
        var hidden = new List<RemovedPage>(_pagesWithRemoved.Count);
        foreach (var pageIndex in _pagesWithRemoved)
        {
            if (GetPage(pageIndex) is not { } page || PdfPageAnnotations.GetArray(_store, page) is not { } annotations)
            {
                continue;
            }

            var kept = new PdfArray(_store, annotations.Count);
            for (var i = 0; i < annotations.Count; i++)
            {
                if (annotations.GetDictionary(i) is not { } annotation || !IsRemoved(annotation))
                {
                    kept.Add(annotations.GetRaw(i));
                }
            }

            if (kept.Count != annotations.Count && PdfPageAnnotations.SetArray(_store, page, kept))
            {
                hidden.Add(new(page, annotations));
            }
        }

        return hidden;
    }

    /// <summary>Puts back the annotations taken off for saving.</summary>
    /// <param name="hidden">The pages and their full arrays.</param>
    private void Restore(List<RemovedPage> hidden)
    {
        foreach (var (page, annotations) in hidden)
        {
            _ = PdfPageAnnotations.SetArray(_store, page, annotations);
        }
    }

    /// <summary>A page whose removed annotations were taken off for saving, and its full annotation array.</summary>
    /// <param name="Page">The page.</param>
    /// <param name="Annotations">The full array.</param>
    private sealed record RemovedPage(PdfPage Page, PdfArray Annotations);
}
