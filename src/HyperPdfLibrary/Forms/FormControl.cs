// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>One widget of a field: its dictionary and, when it is an indirect object, its id.</summary>
/// <param name="Dictionary">The widget dictionary.</param>
/// <param name="Id">The object id; not valid for a widget written directly inside another object.</param>
[DebuggerDisplay("FormControl: {Id}")]
internal sealed record FormControl(PdfDictionary Dictionary, PdfObjectId Id)
{
    /// <summary>Lists the widgets that belong to a field: its widget kids, or the field itself when it is a widget.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="fieldId">The field's object id.</param>
    /// <returns>The controls in kid order.</returns>
    internal static List<FormControl> GetControls(PdfDictionary field, PdfObjectId fieldId)
    {
        var controls = new List<FormControl>();
        if (field.GetArray(KnownName.Kids) is not { } kids)
        {
            if (field.IsName(KnownName.Subtype, KnownName.Widget))
            {
                controls.Add(new(field, fieldId));
            }

            return controls;
        }

        for (var i = 0; i < kids.Count; i++)
        {
            if (kids.GetDictionary(i) is { } kid && kid.IsName(KnownName.Subtype, KnownName.Widget))
            {
                controls.Add(new(kid, kids.GetRaw(i).AsReference()));
            }
        }

        return controls;
    }

    /// <summary>Determines whether the widget is switched on.</summary>
    /// <param name="names">The document's names.</param>
    /// <returns><see langword="true"/> when the appearance state is the on-state.</returns>
    internal bool IsChecked(PdfNameTable names)
    {
        var on = FieldAttributes.GetOnState(Dictionary, names);
        return on.Length > 0 && string.Equals(FieldAttributes.ReadState(Dictionary.Get(KnownName.AS), names), on, StringComparison.Ordinal);
    }
}
