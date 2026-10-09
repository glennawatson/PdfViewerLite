// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>One field's name and value in a form submission.</summary>
/// <param name="Name">The field's full name.</param>
/// <param name="Value">The value: the text, the selected option's value, or for a button the export value of the checked button, or "Off".</param>
[DebuggerDisplay("PdfSubmittedField: {Name} = {Value}")]
public readonly record struct PdfSubmittedField(string Name, string Value);
