// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Reads the attributes that matter for reading from an element's <c>/A</c> attribute objects, then from the classes its
/// <c>/C</c> names in the class map. The first value found for a key wins, so the element's own attributes override its classes.
/// </summary>
[DebuggerDisplay("StructureAttributeReader")]
internal sealed class StructureAttributeReader
{
    /// <summary>The largest row or column span accepted.</summary>
    private const int MaxSpan = 1000;

    /// <summary>The most header ids read from one cell.</summary>
    private const int MaxHeaders = 256;

    /// <summary>The interned structure names.</summary>
    private readonly TaggedNames _names;

    /// <summary>The document's name table.</summary>
    private readonly PdfNameTable _table;

    /// <summary>The class map, or <see langword="null"/>.</summary>
    private readonly PdfDictionary? _classMap;

    /// <summary>Whether the element has any attribute object.</summary>
    private bool _found;

    /// <summary>The Layout placement found so far.</summary>
    private string? _placement;

    /// <summary>The Layout bounding box found so far.</summary>
    private PdfRectangle? _box;

    /// <summary>The Table scope found so far.</summary>
    private PdfTableScope? _scope;

    /// <summary>The Table headers found so far.</summary>
    private string[]? _headers;

    /// <summary>The Table row span found so far.</summary>
    private int? _rowSpan;

    /// <summary>The Table column span found so far.</summary>
    private int? _columnSpan;

    /// <summary>The List numbering found so far.</summary>
    private string? _numbering;

    /// <summary>The Table summary found so far.</summary>
    private string? _summary;

    /// <summary>Initializes a new instance of the <see cref="StructureAttributeReader"/> class.</summary>
    /// <param name="names">The interned structure names.</param>
    /// <param name="table">The document's name table.</param>
    /// <param name="classMap">The structure tree root's <c>/ClassMap</c>, or <see langword="null"/>.</param>
    internal StructureAttributeReader(TaggedNames names, PdfNameTable table, PdfDictionary? classMap)
    {
        _names = names;
        _table = table;
        _classMap = classMap;
    }

    /// <summary>Reads an element's attributes.</summary>
    /// <param name="element">The element's dictionary.</param>
    /// <returns>The attributes; <see cref="PdfStructureAttributes.None"/> when it gives none.</returns>
    internal PdfStructureAttributes Read(PdfDictionary element)
    {
        var own = element.Get(KnownName.A);
        var classes = element.Get(KnownName.C);
        if (own.IsNull && (classes.IsNull || _classMap is null))
        {
            return PdfStructureAttributes.None;
        }

        Reset();
        ApplyAll(own);
        ApplyClasses(classes);
        return Build();
    }

    /// <summary>Reads a span, clamped to a sane range.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The span, or <see langword="null"/> when not given.</returns>
    private static int? ReadSpan(PdfDictionary attributes, PdfName key)
    {
        var value = attributes.Get(key);
        return value.IsNumber ? Math.Clamp(value.AsInt32(), 1, MaxSpan) : null;
    }

    /// <summary>Reads the ids of a cell's header cells.</summary>
    /// <param name="array">The <c>/Headers</c> array of byte strings.</param>
    /// <returns>The ids, or <see langword="null"/> when not given.</returns>
    private static string[]? ReadHeaders(PdfArray? array)
    {
        if (array is null)
        {
            return null;
        }

        var count = Math.Min(array.Count, MaxHeaders);
        var ids = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var item = array.Get(i);
            if (item.Kind == PdfKind.String)
            {
                ids.Add(PdfText.Decode(item.AsStringBytes()));
            }
        }

        return [.. ids];
    }

    /// <summary>Clears what the previous element set.</summary>
    private void Reset()
    {
        _found = false;
        _placement = null;
        _box = null;
        _scope = null;
        _headers = null;
        _rowSpan = null;
        _columnSpan = null;
        _numbering = null;
        _summary = null;
    }

    /// <summary>Makes the attributes from what was found.</summary>
    /// <returns>The attributes.</returns>
    private PdfStructureAttributes Build() => !_found
        ? PdfStructureAttributes.None
        : new(_placement ?? string.Empty, _box, _scope ?? PdfTableScope.None, _headers ?? [], _rowSpan ?? 1, _columnSpan ?? 1, _numbering ?? string.Empty, _summary ?? string.Empty);

    /// <summary>Applies the classes an element names.</summary>
    /// <param name="classes">The <c>/C</c> value: a name, or an array of names and revision numbers.</param>
    private void ApplyClasses(PdfValue classes)
    {
        if (_classMap is null)
        {
            return;
        }

        if (classes.TryGetName(out var single))
        {
            ApplyAll(_classMap.Get(single));
            return;
        }

        if (classes.AsArray() is not { } array)
        {
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array.Get(i).TryGetName(out var name))
            {
                ApplyAll(_classMap.Get(name));
            }
        }
    }

    /// <summary>Applies an attribute object, or each one of an array of them; revision numbers in the array are skipped.</summary>
    /// <param name="value">A dictionary, a stream or an array.</param>
    private void ApplyAll(PdfValue value)
    {
        if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array.Get(i).AsDictionary() is { } item)
                {
                    Apply(item);
                }
            }

            return;
        }

        if (value.AsDictionary() is { } dictionary)
        {
            Apply(dictionary);
        }
    }

    /// <summary>Applies one attribute object.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    private void Apply(PdfDictionary attributes)
    {
        _found = true;
        _placement ??= ReadName(attributes, _names.Placement);
        _numbering ??= ReadName(attributes, _names.ListNumbering);
        _summary ??= attributes.GetText(_names.Summary);
        _scope ??= ReadScope(attributes);
        _headers ??= ReadHeaders(attributes.GetArray(_names.Headers));
        _rowSpan ??= ReadSpan(attributes, _names.RowSpan);
        _columnSpan ??= ReadSpan(attributes, _names.ColSpan);
        if (_box is null && attributes.TryGetRectangle(KnownName.BBox, out var box))
        {
            _box = box;
        }
    }

    /// <summary>Reads a name value as text.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The name, or <see langword="null"/>.</returns>
    private string? ReadName(PdfDictionary attributes, PdfName key) =>
        attributes.Get(key).TryGetName(out var name) ? _table.GetString(name) : null;

    /// <summary>Reads a header cell's scope.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <returns>The scope, or <see langword="null"/> when not given.</returns>
    private PdfTableScope? ReadScope(PdfDictionary attributes)
    {
        if (!attributes.Get(_names.Scope).TryGetName(out var scope))
        {
            return null;
        }

        if (scope == _names.Row)
        {
            return PdfTableScope.Row;
        }

        if (scope == _names.Column)
        {
            return PdfTableScope.Column;
        }

        return scope == _names.Both ? PdfTableScope.Both : null;
    }
}
