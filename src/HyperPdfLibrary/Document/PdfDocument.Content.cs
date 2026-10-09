// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Finds content that needs more than a page viewer: XFA forms, scripts, multimedia and 3D.</content>
public sealed partial class PdfDocument
{
    /// <summary>The most /Parent steps followed to find a field's inherited actions.</summary>
    private const int MaxFieldDepth = 32;

    /// <summary>Gets a value indicating whether the document has an AcroForm.</summary>
    public bool HasAcroForm => Catalog.GetDictionary(KnownName.AcroForm) is not null;

    /// <summary>Gets a value indicating whether the AcroForm carries an XFA form.</summary>
    public bool HasXfaForm => Catalog.GetDictionary(KnownName.AcroForm) is { } form && !form.GetRaw(KnownName.XFA).IsNull;

    /// <summary>Counts the document-level JavaScript actions in the /Names /JavaScript name tree.</summary>
    /// <returns>The number of named scripts.</returns>
    public int GetJavaScriptActionCount()
    {
        if (Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.JavaScript) is not { } tree)
        {
            return 0;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(tree, entries);
        return entries.Count;
    }

    /// <summary>Finds the annotations on a page that need a media player or a 3D viewer.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The content found; none for a page that does not exist.</returns>
    public PdfAnnotationContent ScanAnnotations(int pageIndex)
    {
        var content = PdfAnnotationContent.None;
        if ((uint)pageIndex >= (uint)PageCount || GetPage(pageIndex).Dictionary.GetArray(KnownName.Annots) is not { } annots)
        {
            return content;
        }

        for (var i = 0; i < annots.Count; i++)
        {
            if (annots.GetDictionary(i) is { } annotation)
            {
                content |= Classify(annotation.GetName(KnownName.Subtype));
            }
        }

        return content;
    }

    /// <summary>Appends the JavaScript of the keystroke, format, validate and calculate actions of a page's form fields.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving each non-empty script.</param>
    public void GetWidgetScripts(int pageIndex, List<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if ((uint)pageIndex >= (uint)PageCount || GetPage(pageIndex).Dictionary.GetArray(KnownName.Annots) is not { } annots)
        {
            return;
        }

        for (var i = 0; i < annots.Count; i++)
        {
            AddWidgetScripts(annots.GetDictionary(i), output);
        }
    }

    /// <summary>Says what a annotation subtype needs.</summary>
    /// <param name="subtype">The annotation's /Subtype.</param>
    /// <returns>The content it stands for.</returns>
    private static PdfAnnotationContent Classify(PdfName subtype) => subtype.ToKnownName() switch
    {
        KnownName.Sound or KnownName.Movie or KnownName.Screen or KnownName.RichMedia => PdfAnnotationContent.Multimedia,
        KnownName.ThreeD => PdfAnnotationContent.ThreeD,
        _ => PdfAnnotationContent.None,
    };

    /// <summary>Appends the scripts of one annotation when it is a form widget.</summary>
    /// <param name="annotation">The annotation, or <see langword="null"/>.</param>
    /// <param name="output">The list receiving each non-empty script.</param>
    private static void AddWidgetScripts(PdfDictionary? annotation, List<string> output)
    {
        if (annotation is null || !annotation.IsName(KnownName.Subtype, KnownName.Widget) || GetAdditionalActions(annotation) is not { } actions)
        {
            return;
        }

        AddScript(actions, KnownName.K, output);
        AddScript(actions, KnownName.F, output);
        AddScript(actions, KnownName.V, output);
        AddScript(actions, KnownName.C, output);
    }

    /// <summary>Finds a field's /AA dictionary, inherited from its parents.</summary>
    /// <param name="field">The field or widget.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    private static PdfDictionary? GetAdditionalActions(PdfDictionary field)
    {
        var node = field;
        for (var depth = 0; node is not null && depth < MaxFieldDepth; depth++)
        {
            if (node.GetDictionary(KnownName.AA) is { } actions)
            {
                return actions;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return null;
    }

    /// <summary>Appends the JavaScript of one additional action, when it has some.</summary>
    /// <param name="actions">The /AA dictionary.</param>
    /// <param name="key">The action's key.</param>
    /// <param name="output">The list receiving the script.</param>
    private static void AddScript(PdfDictionary actions, KnownName key, List<string> output)
    {
        var script = actions.GetDictionary(key)?.Get(KnownName.JS) ?? default;
        var text = script.Kind switch
        {
            PdfKind.String => PdfText.Decode(script.AsStringBytes()),
            PdfKind.Stream => PdfText.Decode(script.AsStream()!.DecodeToArray()),
            _ => string.Empty,
        };

        if (text.Length > 0)
        {
            output.Add(text);
        }
    }
}
