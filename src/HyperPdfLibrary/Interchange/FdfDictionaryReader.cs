// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Reads the <c>/FDF</c> dictionary of an FDF file into a <see cref="PdfInterchangeData"/>.</summary>
internal static partial class FdfDictionaryReader
{
    /// <summary>Reads the dictionary.</summary>
    /// <param name="fdf">The <c>/FDF</c> dictionary.</param>
    /// <param name="data">The data to fill.</param>
    internal static void Read(PdfDictionary fdf, PdfInterchangeData data)
    {
        var names = fdf.Owner!.Names;
        data.FileHref = ReadFileSpec(fdf.Get(KnownName.F), names);
        data.Status = fdf.GetText(names.Intern("Status"));
        data.Encoding = fdf.GetName(KnownName.Encoding) is { IsNone: false } encoding ? names.GetString(encoding) : null;
        ReadIds(fdf, data);
        data.JavaScript = ReadScripts(fdf.GetDictionary(KnownName.JavaScript), names);
        data.Differences = fdf.GetStream(KnownName.Differences)?.DecodeToArray() ?? [];
        if (fdf.GetArray(KnownName.Fields) is { } fields)
        {
            ReadFields(fields, string.Empty, data, new());
        }

        if (fdf.GetArray(KnownName.Annots) is { } annotations)
        {
            ReadAnnotations(annotations, data);
        }
    }

    /// <summary>Reads a file specification: a string, or a dictionary holding one.</summary>
    /// <param name="value">The resolved value.</param>
    /// <param name="names">The names.</param>
    /// <returns>The file name, or <see langword="null"/>.</returns>
    private static string? ReadFileSpec(PdfValue value, PdfNameTable names)
    {
        if (value.Kind == PdfKind.String)
        {
            return PdfText.Decode(value.AsStringBytes());
        }

        return value.AsDictionary() is { } spec ? spec.GetText(names.Intern("UF")) ?? spec.GetText(KnownName.F) : null;
    }

    /// <summary>Reads the file identifiers as hexadecimal.</summary>
    /// <param name="fdf">The dictionary.</param>
    /// <param name="data">The data.</param>
    private static void ReadIds(PdfDictionary fdf, PdfInterchangeData data)
    {
        if (fdf.GetArray(KnownName.ID) is not { Count: > 0 } ids)
        {
            return;
        }

        data.OriginalId = Convert.ToHexString(ids.Get(0).AsStringBytes());
        data.ModifiedId = ids.Count > 1 ? Convert.ToHexString(ids.Get(1).AsStringBytes()) : null;
    }

    /// <summary>Reads the <c>/JavaScript</c> dictionary as data.</summary>
    /// <param name="scripts">The dictionary, or <see langword="null"/>.</param>
    /// <param name="names">The names.</param>
    /// <returns>The scripts, or <see langword="null"/> when there are none.</returns>
    private static PdfInterchangeScripts? ReadScripts(PdfDictionary? scripts, PdfNameTable names)
    {
        if (scripts is null)
        {
            return null;
        }

        var document = new List<string>();
        if (scripts.GetArray(names.Intern("Doc")) is { } pairs)
        {
            for (var i = 0; i < pairs.Count; i++)
            {
                document.Add(FieldAttributes.ReadText(pairs.Get(i)));
            }
        }

        return new(
            ReadScript(scripts, names.Intern("Before")),
            ReadScript(scripts, names.Intern("After")),
            ReadScript(scripts, names.Intern("AfterPermsReady")),
            [.. document]);
    }

    /// <summary>Reads a script that is a string or a stream.</summary>
    /// <param name="scripts">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The script, or <see langword="null"/>.</returns>
    private static string? ReadScript(PdfDictionary scripts, PdfName key) =>
        scripts.Get(key) is { Kind: PdfKind.String or PdfKind.Stream } value ? FieldAttributes.ReadText(value) : null;
}
