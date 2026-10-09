// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>Sends a command to the content of a rich media annotation.</summary>
/// <param name="Target">The rich media annotation dictionary (<c>/TA</c>), or null.</param>
/// <param name="Instance">The rich media instance dictionary (<c>/TI</c>), or null.</param>
/// <param name="Command">The command name (<c>/CMD /C</c>), or null.</param>
/// <param name="Arguments">The command arguments (<c>/CMD /A</c>), or null.</param>
[DebuggerDisplay("RichMediaExecuteAction: {Command}")]
public sealed record RichMediaExecuteAction(PdfDictionary? Target, PdfDictionary? Instance, string? Command, PdfArray? Arguments);
