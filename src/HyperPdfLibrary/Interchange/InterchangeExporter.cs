// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Reads the form values and annotations of a document into a <see cref="PdfInterchangeData"/>.</summary>
internal static class InterchangeExporter
{
    /// <summary>The prefix of the names given to annotations that replies refer to but that have none.</summary>
    private const string GeneratedPrefix = "hyperpdf-";

    /// <summary>Reads a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="content">What to read.</param>
    /// <returns>The data.</returns>
    internal static PdfInterchangeData Export(PdfDocument document, PdfInterchangeContent content)
    {
        var data = new PdfInterchangeData();
        ReadIds(document.Objects, data);
        if ((content & PdfInterchangeContent.Fields) != 0)
        {
            InterchangeFieldExporter.Export(document, data);
        }

        if ((content & PdfInterchangeContent.Annotations) != 0)
        {
            ExportAnnotations(document, data);
        }

        return data;
    }

    /// <summary>Reads the file identifiers from the trailer.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="data">The data.</param>
    private static void ReadIds(PdfObjectStore store, PdfInterchangeData data)
    {
        if (store.Trailer.GetArray(KnownName.ID) is not { Count: > 0 } ids)
        {
            return;
        }

        data.OriginalId = Convert.ToHexString(ids.Get(0).AsStringBytes());
        data.ModifiedId = ids.Count > 1 ? Convert.ToHexString(ids.Get(1).AsStringBytes()) : null;
    }

    /// <summary>Reads every page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="data">The data.</param>
    private static void ExportAnnotations(PdfDocument document, PdfInterchangeData data)
    {
        var store = document.Objects;
        var names = NameRepliedTo(document);
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            var page = PdfDocumentPages.GetPage(document, pageIndex);
            if (PdfPageAnnotations.GetArray(store, page) is not { } array)
            {
                continue;
            }

            for (var i = 0; i < array.Count; i++)
            {
                if (array.GetDictionary(i) is { } dictionary
                    && InterchangeAnnotationReader.Read(dictionary, array.GetRaw(i).AsReference(), pageIndex, names) is { } annotation)
                {
                    data.Annotations.Add(annotation);
                }
            }
        }
    }

    /// <summary>Gives a name to every annotation that is answered but has no name of its own.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The names by object number.</returns>
    private static Dictionary<int, string> NameRepliedTo(PdfDocument document)
    {
        var names = new Dictionary<int, string>();
        for (var pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
        {
            if (PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, pageIndex)) is not { } array)
            {
                continue;
            }

            for (var i = 0; i < array.Count; i++)
            {
                if (array.GetDictionary(i) is { } dictionary && dictionary.GetRaw(KnownName.IRT) is { IsReference: true } reply)
                {
                    _ = names.TryAdd(reply.AsReference().Number, GeneratedPrefix + reply.AsReference().Number.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        return names;
    }
}
