// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>A form field widget on a page, with the field's state at one moment.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The widget's index in the page's <c>/Annots</c> array.</param>
/// <param name="Name">The field's full name; empty when the widget belongs to no named field.</param>
/// <param name="Type">The field type.</param>
/// <param name="Bounds">The widget's <c>/Rect</c> in user space.</param>
/// <param name="Value">The text, or the selected option's value; for a button group the export value of the checked button, or "Off".</param>
/// <param name="IsChecked">Whether this check box or radio button is on.</param>
/// <param name="Options">The option labels of a combo or list box, otherwise empty.</param>
/// <param name="SelectedOption">The index of the first selected option, or -1.</param>
/// <param name="Flags">The field flags.</param>
[DebuggerDisplay("PdfFormWidget: {Type} {Name} = {Value}")]
public sealed record PdfFormWidget(
    int PageIndex,
    int Index,
    string Name,
    PdfFieldType Type,
    PdfRectangle Bounds,
    string Value,
    bool IsChecked,
    string[] Options,
    int SelectedOption,
    PdfFieldFlags Flags)
{
    /// <summary>Gets the most characters a text field takes, or 0 for no limit.</summary>
    public int MaxLength { get; init; }

    /// <summary>Gets the size the field's text is drawn at, or 0 when it fits the text to the field.</summary>
    public float FontSize { get; init; }

    /// <summary>Gets a value indicating whether a text field is split into <see cref="MaxLength"/> boxes.</summary>
    public bool IsComb => Type == PdfFieldType.Text && MaxLength > 0 && (Flags & PdfFieldFlags.Comb) != 0;

    /// <summary>Gets a value indicating whether the field cannot be changed.</summary>
    public bool IsReadOnly => (Flags & PdfFieldFlags.ReadOnly) != 0;

    /// <summary>Gets a value indicating whether the field must be filled in.</summary>
    public bool IsRequired => (Flags & PdfFieldFlags.Required) != 0;

    /// <summary>Gets a value indicating whether the field takes several lines.</summary>
    public bool IsMultiline => (Flags & PdfFieldFlags.Multiline) != 0;
}
