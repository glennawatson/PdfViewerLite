// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Opens a URI.</summary>
/// <param name="Uri">The URI, as written in the document.</param>
[DebuggerDisplay("UriAction: {Uri}")]
public sealed record UriAction(string Uri);
