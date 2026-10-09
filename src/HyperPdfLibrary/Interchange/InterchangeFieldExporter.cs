// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Reads the values of a document's form fields by walking the AcroForm field tree.</summary>
internal static class InterchangeFieldExporter
{
    /// <summary>Reads the fields.</summary>
    /// <param name="document">The document.</param>
    /// <param name="data">The data to fill.</param>
    internal static void Export(PdfDocument document, PdfInterchangeData data)
    {
        if (document.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields) is not { } fields)
        {
            return;
        }

        Walk(fields, string.Empty, document.Objects.Names, data, new(), 0);
    }

    /// <summary>Walks an array of fields.</summary>
    /// <param name="fields">The array.</param>
    /// <param name="prefix">The parent's fully qualified name.</param>
    /// <param name="names">The names.</param>
    /// <param name="data">The data.</param>
    /// <param name="seen">The objects already read.</param>
    /// <param name="depth">How deeply the walk has gone.</param>
    private static void Walk(PdfArray fields, string prefix, PdfNameTable names, PdfInterchangeData data, HashSet<int> seen, int depth)
    {
        if (depth >= PdfLimits.MaxNesting)
        {
            return;
        }

        for (var i = 0; i < fields.Count; i++)
        {
            if (fields.GetDictionary(i) is { } field && IsNew(seen, fields.GetRaw(i)))
            {
                Visit(field, prefix, names, data, seen, depth);
            }
        }
    }

    /// <summary>Reads one field, or walks its kids when they are fields of their own.</summary>
    /// <param name="field">The field.</param>
    /// <param name="prefix">The parent's fully qualified name.</param>
    /// <param name="names">The names.</param>
    /// <param name="data">The data.</param>
    /// <param name="seen">The objects already read.</param>
    /// <param name="depth">How deeply the walk has gone.</param>
    private static void Visit(PdfDictionary field, string prefix, PdfNameTable names, PdfInterchangeData data, HashSet<int> seen, int depth)
    {
        var full = InterchangeFieldTree.Join(prefix, field.GetText(KnownName.T) ?? string.Empty);
        if (field.GetArray(KnownName.Kids) is { } kids && HasNamedKid(kids))
        {
            Walk(kids, full, names, data, seen, depth + 1);
        }
        else if (full.Length > 0 && Read(field, full, names) is { } result)
        {
            data.Fields.Add(result);
        }
    }

    /// <summary>Records an object as read.</summary>
    /// <param name="seen">The objects already read.</param>
    /// <param name="raw">The array entry, unresolved.</param>
    /// <returns><see langword="true"/> when the object is new; direct objects are always new.</returns>
    private static bool IsNew(HashSet<int> seen, PdfValue raw) => !raw.IsReference || seen.Add(raw.AsReference().Number);

    /// <summary>Determines whether any kid is a field of its own rather than a widget.</summary>
    /// <param name="kids">The kids.</param>
    /// <returns><see langword="true"/> when a kid has a partial name.</returns>
    private static bool HasNamedKid(PdfArray kids)
    {
        for (var i = 0; i < kids.Count; i++)
        {
            if (kids.GetDictionary(i) is { } kid && kid.ContainsKey(KnownName.T))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads a terminal field's value.</summary>
    /// <param name="field">The field.</param>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="names">The names.</param>
    /// <returns>The field; <see langword="null"/> for a field with no value or one that holds no data, such as a push button or signature.</returns>
    private static PdfInterchangeField? Read(PdfDictionary field, string name, PdfNameTable names)
    {
        var flags = FieldAttributes.GetFlags(field);
        var type = FormValues.GetType(field, names, flags);
        var value = FieldAttributes.Find(field, KnownName.V);
        if (type is PdfFieldType.Unknown or PdfFieldType.PushButton or PdfFieldType.Signature || value.IsNull)
        {
            return null;
        }

        var rich = FieldAttributes.Find(field, names.Intern("RV"));
        return new(name, FdfDictionaryReader.ReadValues(value, names))
        {
            ValueIsName = value.Kind == PdfKind.Name,
            RichText = rich.Kind is PdfKind.String or PdfKind.Stream ? FieldAttributes.ReadText(rich) : null,
        };
    }
}
