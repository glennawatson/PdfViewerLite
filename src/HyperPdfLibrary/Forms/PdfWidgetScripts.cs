// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>The JavaScript of a widget's additional actions, which the library never runs.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The widget's index in the page's <c>/Annots</c> array.</param>
/// <param name="Name">The field's full name.</param>
/// <param name="Keystroke">The keystroke action's script (<c>/K</c>), or empty.</param>
/// <param name="Format">The format action's script (<c>/F</c>), or empty.</param>
/// <param name="Validate">The validate action's script (<c>/V</c>), or empty.</param>
/// <param name="Calculate">The calculate action's script (<c>/C</c>), or empty.</param>
[DebuggerDisplay("PdfWidgetScripts: {Name}")]
public sealed record PdfWidgetScripts(int PageIndex, int Index, string Name, string Keystroke, string Format, string Validate, string Calculate);
