// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Goes to a page in a PDF embedded in this one.</summary>
/// <param name="EmbeddedFile">The embedded file's name in the /EmbeddedFiles tree, when given.</param>
[DebuggerDisplay("EmbeddedGoToAction: {EmbeddedFile}")]
public sealed record EmbeddedGoToAction(string? EmbeddedFile);
