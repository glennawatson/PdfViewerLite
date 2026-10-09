// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <content>The <c>/Fields</c> array.</content>
internal static partial class FdfDictionaryReader
{
    /// <summary>Reads a field's value as text.</summary>
    /// <param name="value">The resolved <c>/V</c>.</param>
    /// <param name="names">The names.</param>
    /// <returns>The values: none for no value, several for an array.</returns>
    internal static string[] ReadValues(PdfValue value, PdfNameTable names)
    {
        if (value.AsArray() is not { } array)
        {
            return value.IsNull ? [] : [ReadOne(value, names)];
        }

        var values = new string[array.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadOne(array.Get(i), names);
        }

        return values;
    }

    /// <summary>Reads an array of field dictionaries and their kids.</summary>
    /// <param name="fields">The array.</param>
    /// <param name="prefix">The fully qualified name of the parent field; empty at the top.</param>
    /// <param name="data">The data.</param>
    /// <param name="walk">The walk's bounds.</param>
    /// <exception cref="PdfException">The fields nest deeper than the limit.</exception>
    private static void ReadFields(PdfArray fields, string prefix, PdfInterchangeData data, FieldWalk walk)
    {
        if (walk.Depth >= PdfLimits.MaxNesting)
        {
            throw new PdfException(PdfError.Format, "The FDF fields nest too deeply.");
        }

        walk.Depth++;
        for (var i = 0; i < fields.Count; i++)
        {
            // An object reached twice, as in a cycle, is read once.
            if (fields.GetDictionary(i) is { } field && walk.Visit(fields.GetRaw(i)))
            {
                ReadField(field, prefix, data, walk);
            }
        }

        walk.Depth--;
    }

    /// <summary>Reads one field dictionary.</summary>
    /// <param name="field">The field.</param>
    /// <param name="prefix">The parent's fully qualified name.</param>
    /// <param name="data">The data.</param>
    /// <param name="walk">The walk's bounds.</param>
    private static void ReadField(PdfDictionary field, string prefix, PdfInterchangeData data, FieldWalk walk)
    {
        var full = InterchangeFieldTree.Join(prefix, field.GetText(KnownName.T) ?? string.Empty);
        var names = field.Owner!.Names;
        var result = ReadValue(field, full, names);
        if (full.Length > 0 && result is not null)
        {
            data.Fields.Add(result);
        }

        if (field.GetArray(KnownName.Kids) is { } kids)
        {
            ReadFields(kids, full, data, walk);
        }
    }

    /// <summary>Reads a field's value and flags.</summary>
    /// <param name="field">The field.</param>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="names">The names.</param>
    /// <returns>The field; <see langword="null"/> when it holds no value and changes no flags.</returns>
    private static PdfInterchangeField? ReadValue(PdfDictionary field, string name, PdfNameTable names)
    {
        var value = field.Get(KnownName.V);
        var rich = field.Get(names.Intern("RV"));
        var flags = ReadFlags(field, KnownName.Ff);
        var set = ReadFlags(field, names.Intern("SetFf"));
        var clear = ReadFlags(field, names.Intern("ClrFf"));
        var text = rich.Kind is PdfKind.String or PdfKind.Stream ? FieldAttributes.ReadText(rich) : null;
        return value.IsNull && text is null && flags is null && set is null && clear is null
            ? null
            : new PdfInterchangeField(name, ReadValues(value, names)) { RichText = text, ValueIsName = value.Kind == PdfKind.Name, Flags = flags, SetFlags = set, ClearFlags = clear, };
    }

    /// <summary>Reads one value: a string, a stream of text or a name.</summary>
    /// <param name="value">The resolved value.</param>
    /// <param name="names">The names.</param>
    /// <returns>The text.</returns>
    private static string ReadOne(PdfValue value, PdfNameTable names) =>
        value.Kind == PdfKind.Name ? names.GetString(value.AsName()) : FieldAttributes.ReadText(value);

    /// <summary>Reads a flags entry.</summary>
    /// <param name="field">The field.</param>
    /// <param name="key">The key.</param>
    /// <returns>The flags, or <see langword="null"/> when absent.</returns>
    private static uint? ReadFlags(PdfDictionary field, PdfName key) =>
        field.Get(key).IsNumber ? (uint)field.GetInteger(key) : null;

    /// <summary>Bounds a walk over the field tree.</summary>
    private sealed class FieldWalk
    {
        /// <summary>The objects already read.</summary>
        private readonly HashSet<int> _seen = [];

        /// <summary>Gets or sets how deeply the walk has gone.</summary>
        internal int Depth { get; set; }

        /// <summary>Records an object as read.</summary>
        /// <param name="raw">The array entry, unresolved.</param>
        /// <returns><see langword="true"/> when the object is new; direct objects are always new.</returns>
        internal bool Visit(PdfValue raw) => !raw.IsReference || _seen.Add(raw.AsReference().Number);
    }
}
