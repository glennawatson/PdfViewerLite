// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>A document-level script from the <c>/Names /JavaScript</c> name tree.</summary>
/// <param name="Name">The script's name.</param>
/// <param name="Script">The script text.</param>
[DebuggerDisplay("PdfNamedScript: {Name}")]
public sealed record PdfNamedScript(string Name, string Script);
