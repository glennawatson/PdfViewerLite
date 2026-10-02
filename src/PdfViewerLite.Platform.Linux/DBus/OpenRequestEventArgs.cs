// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Platform.Linux.DBus;

/// <summary>A request from another process to open documents or raise the window.</summary>
[DebuggerDisplay("{Uris.Count} uris")]
public sealed class OpenRequestEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="OpenRequestEventArgs"/> class.</summary>
    /// <param name="uris">The URIs or paths to open; empty to just activate.</param>
    /// <param name="activationToken">The XDG activation or startup notification token, if supplied.</param>
    public OpenRequestEventArgs(IReadOnlyList<string> uris, string? activationToken)
    {
        Uris = uris;
        ActivationToken = activationToken;
    }

    /// <summary>Gets the URIs or paths to open.</summary>
    public IReadOnlyList<string> Uris { get; }

    /// <summary>Gets the activation token used to raise the window without focus stealing prevention.</summary>
    public string? ActivationToken { get; }
}
