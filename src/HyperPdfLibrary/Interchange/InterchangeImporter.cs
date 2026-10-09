// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Interchange;

/// <summary>Applies a <see cref="PdfInterchangeData"/> to a document: sets form values and adds annotations.</summary>
internal static class InterchangeImporter
{
    /// <summary>Applies the data.</summary>
    /// <param name="document">The document.</param>
    /// <param name="data">The data.</param>
    /// <returns>What changed.</returns>
    internal static PdfInterchangeImportResult Import(PdfDocument document, PdfInterchangeData data)
    {
        var (applied, skippedFields) = ImportFields(document, data);
        var annotations = InterchangeAnnotationImporter.Import(document, data.Annotations);
        return new(applied, skippedFields, annotations.Added, annotations.Replaced, annotations.Skipped);
    }

    /// <summary>Sets the form values.</summary>
    /// <param name="document">The document.</param>
    /// <param name="data">The data.</param>
    /// <returns>The number of fields set and left alone.</returns>
    private static FieldCounts ImportFields(PdfDocument document, PdfInterchangeData data)
    {
        if (data.Fields.Count == 0)
        {
            return default;
        }

        var form = PdfDocumentForms.GetForm(document);
        var widgets = IndexWidgets(document, form);
        var applied = 0;
        var skipped = 0;
        foreach (var field in data.Fields)
        {
            if (widgets.TryGetValue(field.Name, out var widget) && form.ImportValue(widget.PageIndex, widget.Index, field.Values))
            {
                applied++;
            }
            else
            {
                skipped++;
            }
        }

        return new(applied, skipped);
    }

    /// <summary>Finds the first widget of every named field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="form">The form.</param>
    /// <returns>The widgets by full field name.</returns>
    private static Dictionary<string, PdfFormWidget> IndexWidgets(PdfDocument document, PdfForm form)
    {
        var widgets = new Dictionary<string, PdfFormWidget>(StringComparer.Ordinal);
        if (!form.HasForm)
        {
            return widgets;
        }

        var page = new List<PdfFormWidget>();
        for (var i = 0; i < document.PageCount; i++)
        {
            page.Clear();
            form.GetWidgets(i, page);
            foreach (var widget in page)
            {
                _ = widgets.TryAdd(widget.Name, widget);
            }
        }

        return widgets;
    }

    /// <summary>How many fields were set and how many were left alone.</summary>
    /// <param name="Applied">The fields set.</param>
    /// <param name="Skipped">The fields left alone.</param>
    private readonly record struct FieldCounts(int Applied, int Skipped);
}
