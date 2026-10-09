// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Sorts a document's pending edits into the change kinds DocMDP and FieldMDP permissions name, by comparing each
/// edited object with the one in the file. New objects that are not annotations, fields, pages or signatures (fonts,
/// appearance streams and similar) count only through the objects that use them.
/// </summary>
[DebuggerDisplay("PdfChangeClassifier: {Kinds}")]
internal sealed class PdfChangeClassifier
{
    /// <summary>The deepest field hierarchy followed for names and inherited types.</summary>
    private const int MaxFieldDepth = 32;

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The object numbers of indirect /Annots arrays.</summary>
    private readonly HashSet<int> _annotationArrays = [];

    /// <summary>The object numbers of page content streams.</summary>
    private readonly HashSet<int> _contentStreams = [];

    /// <summary>The catalog's object number.</summary>
    private readonly int _catalog;

    /// <summary>The information dictionary's object number.</summary>
    private readonly int _info;

    /// <summary>The XMP stream's object number.</summary>
    private readonly int _metadata;

    /// <summary>The AcroForm's object number, when indirect.</summary>
    private readonly int _acroForm;

    /// <summary>The AcroForm /Fields array's object number, when indirect.</summary>
    private readonly int _fieldsArray;

    /// <summary>Initializes a new instance of the <see cref="PdfChangeClassifier"/> class.</summary>
    /// <param name="document">The document.</param>
    private PdfChangeClassifier(PdfDocument document)
    {
        _store = document.Objects;
        var catalog = document.Catalog;
        _catalog = _store.Trailer.GetRaw(KnownName.Root).AsReference().Number;
        _info = _store.Trailer.GetRaw(KnownName.Info).AsReference().Number;
        _metadata = catalog.GetRaw(KnownName.Metadata).AsReference().Number;
        var acroForm = catalog.GetRaw(KnownName.AcroForm);
        _acroForm = acroForm.AsReference().Number;
        _fieldsArray = _store.Resolve(acroForm).AsDictionary()?.GetRaw(KnownName.Fields).AsReference().Number ?? 0;
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i).Dictionary;
            AddReference(_annotationArrays, page.GetRaw(KnownName.Annots));
            var contents = page.GetRaw(KnownName.Contents);
            AddReference(_contentStreams, contents);
            var array = _store.Resolve(contents).AsArray();
            for (var j = 0; array is not null && j < array.Count; j++)
            {
                AddReference(_contentStreams, array.GetRaw(j));
            }
        }
    }

    /// <summary>Gets the kinds of change found.</summary>
    internal PdfChangeKinds Kinds { get; private set; }

    /// <summary>Gets the fully qualified names of the fields whose values changed.</summary>
    internal List<string> ChangedFields { get; } = [];

    /// <summary>Gets the object numbers changed, added or deleted.</summary>
    internal HashSet<int> ChangedObjects { get; } = [];

    /// <summary>Gets the keys a widget or field may change while being filled in or signed.</summary>
    private static ReadOnlySpan<KnownName> FillKeys => [KnownName.V, KnownName.AS, KnownName.AP, KnownName.MK, KnownName.M];

    /// <summary>Gets the AcroForm keys filling in or signing may change.</summary>
    private static ReadOnlySpan<KnownName> FormFlagKeys => [KnownName.NeedAppearances, KnownName.SigFlags];

    /// <summary>Gets the page keys an annotation change touches.</summary>
    private static ReadOnlySpan<KnownName> AnnotationKeys => [KnownName.Annots];

    /// <summary>Classifies a document's pending edits.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The classifier, holding the results.</returns>
    internal static PdfChangeClassifier Classify(PdfDocument document)
    {
        var classifier = new PdfChangeClassifier(document);
        foreach (var edited in document.Objects.GetEditedObjects(out _))
        {
            _ = classifier.ChangedObjects.Add(edited.Number);
            var original = document.Objects.GetOriginal(edited.Number);
            if (!PdfValueEquality.Equal(original, edited.Value))
            {
                classifier.Kinds |= classifier.ClassifyObject(edited.Number, original, edited.Value);
            }
        }

        return classifier;
    }

    /// <summary>Gets a field's fully qualified name: the /T of it and its ancestors, joined by dots.</summary>
    /// <param name="field">The field or merged widget.</param>
    /// <returns>The name.</returns>
    internal static string FullName(PdfDictionary field)
    {
        var parts = new List<string>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var node = field; node is not null && parts.Count < MaxFieldDepth && visited.Add(node); node = node.GetDictionary(KnownName.Parent))
        {
            if (node.GetText(KnownName.T) is { } part)
            {
                parts.Add(part);
            }
        }

        parts.Reverse();
        return string.Join('.', parts);
    }

    /// <summary>Gets a field's type, inherited from its ancestors when not its own.</summary>
    /// <param name="field">The field or widget.</param>
    /// <returns>The /FT name.</returns>
    internal static PdfName FieldType(PdfDictionary field)
    {
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var node = field; node is not null && visited.Count < MaxFieldDepth && visited.Add(node); node = node.GetDictionary(KnownName.Parent))
        {
            if (node.GetName(KnownName.FT) is { IsNone: false } type)
            {
                return type;
            }
        }

        return default;
    }

    /// <summary>Adds a value's object number to a set when it is a reference.</summary>
    /// <param name="set">The set.</param>
    /// <param name="value">The value.</param>
    private static void AddReference(HashSet<int> set, PdfValue value)
    {
        if (value.IsReference)
        {
            _ = set.Add(value.AsReference().Number);
        }
    }

    /// <summary>Determines whether a dictionary is an annotation.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> when it is typed /Annot or has a subtype and a rectangle.</returns>
    private static bool IsAnnotation(PdfDictionary dictionary) =>
        dictionary.IsName(KnownName.Type, KnownName.Annot) || (dictionary.ContainsKey(KnownName.Subtype) && dictionary.ContainsKey(KnownName.Rect));

    /// <summary>Determines whether a dictionary is a form field that is not also a widget.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsField(PdfDictionary dictionary) =>
        dictionary.ContainsKey(KnownName.FT) || (dictionary.ContainsKey(KnownName.T) && (dictionary.ContainsKey(KnownName.Kids) || dictionary.ContainsKey(KnownName.Parent)));

    /// <summary>Classifies a page dictionary.</summary>
    /// <param name="original">The page in the file.</param>
    /// <param name="current">The page now.</param>
    /// <returns>Annotations when only /Annots changed, otherwise page changes.</returns>
    private static PdfChangeKinds ClassifyPage(PdfDictionary? original, PdfDictionary? current) =>
        original is null || current is null || PdfValueEquality.DiffersOutside(original, current, AnnotationKeys) ? PdfChangeKinds.PageChanges : PdfChangeKinds.Annotations;

    /// <summary>Classifies an AcroForm change: form flags only, or the form's structure.</summary>
    /// <param name="original">One side.</param>
    /// <param name="current">The other side.</param>
    /// <returns>The kinds.</returns>
    private static PdfChangeKinds ClassifyAcroForm(PdfDictionary? original, PdfDictionary? current) =>
        original is null || current is null || PdfValueEquality.DiffersOutside(original, current, FormFlagKeys) ? PdfChangeKinds.Other : PdfChangeKinds.FormFill;

    /// <summary>Classifies a dictionary that is not a page, annotation or field.</summary>
    /// <param name="type">Its /Type.</param>
    /// <param name="sample">The dictionary.</param>
    /// <param name="addedOrDeleted">Whether the object was added or deleted rather than changed.</param>
    /// <returns>Signing for signature dictionaries, metadata for metadata streams, otherwise other for a change.</returns>
    private static PdfChangeKinds ClassifyOther(KnownName type, PdfDictionary sample, bool addedOrDeleted)
    {
        if (type is KnownName.Sig || sample.ContainsKey(KnownName.ByteRange))
        {
            return PdfChangeKinds.Signing;
        }

        if (type == KnownName.Metadata)
        {
            return PdfChangeKinds.Metadata;
        }

        return addedOrDeleted ? PdfChangeKinds.None : PdfChangeKinds.Other;
    }

    /// <summary>Classifies one edited object.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="original">The value in the file; null for a new object.</param>
    /// <param name="current">The value now; null for a deleted object.</param>
    /// <returns>The kinds.</returns>
    private PdfChangeKinds ClassifyObject(int number, PdfValue original, PdfValue current)
    {
        if (TryClassifyKnown(number, original, current, out var kinds))
        {
            return kinds;
        }

        if (number == _fieldsArray || (current.IsNull ? original : current).AsDictionary() is not { } dictionary)
        {
            return original.IsNull || current.IsNull ? PdfChangeKinds.None : PdfChangeKinds.Other;
        }

        return ClassifyDictionary(dictionary, original.AsDictionary(), current.AsDictionary());
    }

    /// <summary>Classifies an object known by its number: the information dictionary, XMP, catalog, form, annotation lists and content.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="original">The value in the file.</param>
    /// <param name="current">The value now.</param>
    /// <param name="kinds">The kinds.</param>
    /// <returns><see langword="true"/> when the number is a known one.</returns>
    private bool TryClassifyKnown(int number, PdfValue original, PdfValue current, out PdfChangeKinds kinds)
    {
        kinds = number switch
        {
            _ when number == _info || number == _metadata => PdfChangeKinds.Metadata,
            _ when number == _catalog => ClassifyCatalog(original.AsDictionary(), current.AsDictionary()),
            _ when number == _acroForm => ClassifyAcroForm(original.AsDictionary(), current.AsDictionary()),
            _ when _annotationArrays.Contains(number) => PdfChangeKinds.Annotations,
            _ when _contentStreams.Contains(number) => PdfChangeKinds.PageChanges,
            _ => PdfChangeKinds.None,
        };
        return kinds != PdfChangeKinds.None;
    }

    /// <summary>Classifies an edited dictionary or stream by what it is.</summary>
    /// <param name="sample">The current value, or the original for a deleted object.</param>
    /// <param name="original">The value in the file.</param>
    /// <param name="current">The value now.</param>
    /// <returns>The kinds.</returns>
    private PdfChangeKinds ClassifyDictionary(PdfDictionary sample, PdfDictionary? original, PdfDictionary? current)
    {
        var type = sample.GetName(KnownName.Type).ToKnownName();
        if (type is KnownName.Pages or KnownName.Page)
        {
            return type == KnownName.Pages ? PdfChangeKinds.PageChanges : ClassifyPage(original, current);
        }

        if (sample.IsName(KnownName.Subtype, KnownName.Widget) || (IsField(sample) && !IsAnnotation(sample)))
        {
            return ClassifyFormObject(sample, original, current);
        }

        return IsAnnotation(sample) ? PdfChangeKinds.Annotations : ClassifyOther(type, sample, original is null || current is null);
    }

    /// <summary>Classifies a field or widget: filling in or signing when only value keys changed, otherwise form structure.</summary>
    /// <param name="sample">The current value, or the original for a deleted object.</param>
    /// <param name="original">The value in the file.</param>
    /// <param name="current">The value now.</param>
    /// <returns>The kinds.</returns>
    private PdfChangeKinds ClassifyFormObject(PdfDictionary sample, PdfDictionary? original, PdfDictionary? current)
    {
        if (original is null || current is null || PdfValueEquality.DiffersOutside(original, current, FillKeys))
        {
            return PdfChangeKinds.Other;
        }

        ChangedFields.Add(FullName(sample));
        var signing = FieldType(sample).Is(KnownName.Sig) && !PdfValueEquality.KeyEqual(original, current, KnownName.V);
        return signing ? PdfChangeKinds.Signing : PdfChangeKinds.FormFill;
    }

    /// <summary>Classifies an edited catalog key by key.</summary>
    /// <param name="original">The catalog in the file.</param>
    /// <param name="current">The catalog now.</param>
    /// <returns>The kinds.</returns>
    private PdfChangeKinds ClassifyCatalog(PdfDictionary? original, PdfDictionary? current)
    {
        if (original is null || current is null)
        {
            return PdfChangeKinds.Other;
        }

        var kinds = ClassifyCatalogKeys(original, current);
        return kinds | ClassifyCatalogKeys(current, original);
    }

    /// <summary>Classifies the catalog keys of one side that differ on the other.</summary>
    /// <param name="source">The catalog whose keys are checked.</param>
    /// <param name="other">The other catalog.</param>
    /// <returns>The kinds.</returns>
    private PdfChangeKinds ClassifyCatalogKeys(PdfDictionary source, PdfDictionary other)
    {
        var kinds = PdfChangeKinds.None;
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (PdfValueEquality.Equal(source.GetValueAt(i), other.GetRaw(key)))
            {
                continue;
            }

            kinds |= key.ToKnownName() switch
            {
                KnownName.Pages => PdfChangeKinds.PageChanges,
                KnownName.Metadata => PdfChangeKinds.Metadata,
                KnownName.DSS => PdfChangeKinds.Signing,
                KnownName.AcroForm => ClassifyAcroForm(_store.Resolve(source.GetValueAt(i)).AsDictionary(), _store.Resolve(other.GetRaw(key)).AsDictionary()),
                _ => PdfChangeKinds.Other,
            };
        }

        return kinds;
    }
}
