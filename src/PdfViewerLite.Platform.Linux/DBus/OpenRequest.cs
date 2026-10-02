// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Platform.Linux.DBus;

/// <summary>A request from another process to open documents or raise the window.</summary>
/// <param name="Uris">The URIs or paths to open; empty to just activate.</param>
/// <param name="ActivationToken">The XDG activation or startup notification token, if supplied.</param>
[DebuggerDisplay("{Uris.Count} uris")]
public sealed record OpenRequest(IReadOnlyList<string> Uris, string? ActivationToken);
