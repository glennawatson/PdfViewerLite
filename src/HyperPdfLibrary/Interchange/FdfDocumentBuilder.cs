// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Builds the <c>/FDF</c> dictionary of an FDF file, and the objects it refers to, from a <see cref="PdfInterchangeData"/>.</summary>
internal static class FdfDocumentBuilder
{
    /// <summary>The entries a field dictionary has at most.</summary>
    private const int FieldEntries = 8;

    /// <summary>The entries a pop-up dictionary has.</summary>
    private const int PopupEntries = 6;

    /// <summary>The identifiers in <c>/ID</c>.</summary>
    private const int IdEntries = 2;

    /// <summary>The entries of the <c>/JavaScript</c> dictionary.</summary>
    private const int ScriptEntries = 4;

    /// <summary>The entries the FDF dictionary has at most.</summary>
    private const int FdfEntries = 8;

    /// <summary>Builds the dictionary.</summary>
    /// <param name="data">The data.</param>
    /// <param name="names">The names.</param>
    /// <param name="objects">Receives the annotations and streams as indirect objects.</param>
    /// <returns>The <c>/FDF</c> dictionary.</returns>
    internal static PdfDictionary Build(PdfInterchangeData data, PdfNameTable names, FdfObjects objects)
    {
        var fdf = new PdfDictionary(null, FdfEntries);
        SetText(fdf, KnownName.F, data.FileHref);
        SetText(fdf, names.Intern("Status"), data.Status);
        if (!string.IsNullOrEmpty(data.Encoding))
        {
            fdf.Set(KnownName.Encoding, PdfValue.FromName(names.Intern(data.Encoding)));
        }

        SetIds(fdf, data);
        SetScripts(fdf, data.JavaScript, names);
        if (!data.Differences.IsEmpty)
        {
            fdf.Set(KnownName.Differences, objects.Add(new(new PdfDictionary(null), data.Differences.ToArray())));
        }

        if (data.Fields.Count > 0)
        {
            fdf.Set(KnownName.Fields, PdfValue.FromArray(BuildFields(InterchangeFieldTree.Build(data.Fields), names)));
        }

        if (data.Annotations.Count > 0)
        {
            fdf.Set(KnownName.Annots, PdfValue.FromArray(BuildAnnotations(data.Annotations, names, objects)));
        }

        return fdf;
    }

    /// <summary>Builds the array of field dictionaries for a node's children.</summary>
    /// <param name="node">The parent.</param>
    /// <param name="names">The names.</param>
    /// <returns>The array.</returns>
    private static PdfArray BuildFields(InterchangeFieldTree node, PdfNameTable names)
    {
        var array = new PdfArray(null, node.Children.Count);
        foreach (var child in node.Children)
        {
            array.Add(PdfValue.FromDictionary(BuildField(child, names)));
        }

        return array;
    }

    /// <summary>Builds one field dictionary.</summary>
    /// <param name="node">The field.</param>
    /// <param name="names">The names.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary BuildField(InterchangeFieldTree node, PdfNameTable names)
    {
        var dictionary = new PdfDictionary(null, FieldEntries);
        dictionary.Set(KnownName.T, PdfValue.FromString(PdfText.Encode(node.PartialName)));
        if (node.Field is { } field)
        {
            SetValue(dictionary, field, names);
        }

        if (node.Children.Count > 0)
        {
            dictionary.Set(KnownName.Kids, PdfValue.FromArray(BuildFields(node, names)));
        }

        return dictionary;
    }

    /// <summary>Sets a field's value, rich value and flags.</summary>
    /// <param name="dictionary">The field dictionary.</param>
    /// <param name="field">The field.</param>
    /// <param name="names">The names.</param>
    private static void SetValue(PdfDictionary dictionary, PdfInterchangeField field, PdfNameTable names)
    {
        if (field.Values.Length == 1)
        {
            dictionary.Set(KnownName.V, field.ValueIsName ? PdfValue.FromName(names.Intern(field.Values[0])) : ToString(field.Values[0]));
        }
        else if (field.Values.Length > 1)
        {
            var values = new PdfArray(null, field.Values.Length);
            foreach (var value in field.Values)
            {
                values.Add(ToString(value));
            }

            dictionary.Set(KnownName.V, PdfValue.FromArray(values));
        }

        SetText(dictionary, names.Intern("RV"), field.RichText);
        SetFlags(dictionary, KnownName.Ff, field.Flags);
        SetFlags(dictionary, names.Intern("SetFf"), field.SetFlags);
        SetFlags(dictionary, names.Intern("ClrFf"), field.ClearFlags);
    }

    /// <summary>Builds the annotation objects and the array that lists them.</summary>
    /// <param name="annotations">The annotations.</param>
    /// <param name="names">The names.</param>
    /// <param name="objects">Receives the objects.</param>
    /// <returns>The array of references.</returns>
    private static PdfArray BuildAnnotations(List<PdfInterchangeAnnotation> annotations, PdfNameTable names, FdfObjects objects)
    {
        var ids = new Dictionary<string, PdfObjectId>(StringComparer.Ordinal);
        var written = new List<PdfObjectId>(annotations.Count);
        foreach (var annotation in annotations)
        {
            var id = objects.Reserve();
            written.Add(id);
            if (!string.IsNullOrEmpty(annotation.Name))
            {
                ids[annotation.Name] = id;
            }
        }

        var array = new PdfArray(null, annotations.Count);
        for (var i = 0; i < annotations.Count; i++)
        {
            var dictionary = BuildAnnotation(annotations[i], written[i], ids, names, objects);
            objects.Set(written[i], PdfValue.FromDictionary(dictionary));
            array.Add(PdfValue.FromReference(written[i]));
        }

        return array;
    }

    /// <summary>Builds one annotation dictionary with its page, reply link and pop-up.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="id">The annotation's object id.</param>
    /// <param name="ids">The object ids of the named annotations.</param>
    /// <param name="names">The names.</param>
    /// <param name="objects">Receives the pop-up and streams.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary BuildAnnotation(
        PdfInterchangeAnnotation annotation,
        PdfObjectId id,
        Dictionary<string, PdfObjectId> ids,
        PdfNameTable names,
        FdfObjects objects)
    {
        var dictionary = InterchangeAnnotationBuilder.Build(annotation, null, names, objects);
        dictionary.Set(KnownName.Page, PdfValue.FromInteger(annotation.Page));
        if (annotation.InReplyTo is { } parentName && ids.TryGetValue(parentName, out var parent))
        {
            InterchangeAnnotationBuilder.LinkReply(dictionary, parent, annotation.ReplyType, names);
        }

        if (annotation.Popup is { } popup)
        {
            var window = PdfAnnotations.Create(null, KnownName.Popup, popup.Rect);
            window.Set(KnownName.Open, PdfValue.FromBoolean(popup.IsOpen));
            window.Set(KnownName.Parent, PdfValue.FromReference(id));
            window.Set(KnownName.Page, PdfValue.FromInteger(popup.Page));
            dictionary.Set(KnownName.Popup, objects.AddObject(PdfValue.FromDictionary(window)));
        }

        return dictionary;
    }

    /// <summary>Sets <c>/ID</c> from hexadecimal identifiers.</summary>
    /// <param name="fdf">The dictionary.</param>
    /// <param name="data">The data.</param>
    private static void SetIds(PdfDictionary fdf, PdfInterchangeData data)
    {
        if (data.OriginalId is null || InterchangeBytes.DecodeHex(data.OriginalId) is not { } first)
        {
            return;
        }

        var second = data.ModifiedId is null ? null : InterchangeBytes.DecodeHex(data.ModifiedId);
        var ids = new PdfArray(null, IdEntries);
        ids.Add(PdfValue.FromString(first));
        ids.Add(PdfValue.FromString(second ?? first));
        fdf.Set(KnownName.ID, PdfValue.FromArray(ids));
    }

    /// <summary>Sets <c>/JavaScript</c> from the scripts kept as data.</summary>
    /// <param name="fdf">The dictionary.</param>
    /// <param name="scripts">The scripts, or <see langword="null"/>.</param>
    /// <param name="names">The names.</param>
    private static void SetScripts(PdfDictionary fdf, PdfInterchangeScripts? scripts, PdfNameTable names)
    {
        if (scripts is null)
        {
            return;
        }

        var dictionary = new PdfDictionary(null, ScriptEntries);
        SetText(dictionary, names.Intern("Before"), scripts.Before);
        SetText(dictionary, names.Intern("After"), scripts.After);
        SetText(dictionary, names.Intern("AfterPermsReady"), scripts.AfterPermsReady);
        if (scripts.Document.Length > 0)
        {
            var pairs = new PdfArray(null, scripts.Document.Length);
            foreach (var item in scripts.Document)
            {
                pairs.Add(ToString(item));
            }

            dictionary.Set(names.Intern("Doc"), PdfValue.FromArray(pairs));
        }

        fdf.Set(KnownName.JavaScript, PdfValue.FromDictionary(dictionary));
    }

    /// <summary>Makes a text string value.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfValue ToString(string text) => PdfValue.FromString(PdfText.Encode(text));

    /// <summary>Sets a text string entry when there is text.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    private static void SetText(PdfDictionary dictionary, PdfName key, string? text)
    {
        if (text is not null)
        {
            dictionary.Set(key, ToString(text));
        }
    }

    /// <summary>Sets a flags entry when there are flags.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="flags">The flags, or <see langword="null"/>.</param>
    private static void SetFlags(PdfDictionary dictionary, PdfName key, uint? flags)
    {
        if (flags is { } value)
        {
            dictionary.Set(key, PdfValue.FromInteger(value));
        }
    }
}
