// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Forms;

/// <summary>A form field widget on a page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The widget's annotation index on the page.</param>
/// <param name="Name">The field's full name.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Bounds">The bounds in page space (points, top-left origin).</param>
/// <param name="Value">The current value: the text, or the selected option.</param>
/// <param name="IsChecked">Whether a check box or radio button is on.</param>
/// <param name="Options">The choices of a combo or list box, otherwise empty.</param>
/// <param name="SelectedOption">The index of the selected choice, or -1.</param>
/// <param name="IsReadOnly">Whether the field cannot be changed.</param>
/// <param name="IsRequired">Whether the field must be filled in.</param>
/// <param name="IsMultiline">Whether a text field takes several lines.</param>
[DebuggerDisplay("FormField: {Kind} {Name} = {Value}")]
public sealed record FormField(
    int PageIndex,
    int Index,
    string Name,
    FormFieldKind Kind,
    PageRect Bounds,
    string Value,
    bool IsChecked,
    IReadOnlyList<string> Options,
    int SelectedOption,
    bool IsReadOnly,
    bool IsRequired,
    bool IsMultiline);
