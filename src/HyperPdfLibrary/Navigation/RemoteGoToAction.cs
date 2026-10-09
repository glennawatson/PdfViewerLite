// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Goes to a page in another PDF.</summary>
/// <param name="File">The other file, as written in the document.</param>
/// <param name="PageIndex">The zero based page in the other file.</param>
[DebuggerDisplay("RemoteGoToAction: {File} page {PageIndex}")]
public sealed record RemoteGoToAction(string File, int PageIndex)
{
    /// <summary>Gets the name of the destination in the other file, when the action names one instead of giving a page.</summary>
    public string? NamedDestination { get; init; }
}
