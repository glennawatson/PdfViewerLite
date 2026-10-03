// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Signatures.Signing;

/// <summary>A PDF file's bytes and the object locations read from its cross-reference sections.</summary>
/// <param name="File">The file.</param>
/// <param name="Entries">Where each object lives, newest definition only.</param>
/// <param name="Trailer">The newest trailer dictionary, as its own bytes.</param>
/// <param name="StartXref">The offset of the newest cross-reference section.</param>
[DebuggerDisplay("{Entries.Count} objects")]
internal sealed record PdfStructure(byte[] File, Dictionary<int, XrefEntry> Entries, byte[] Trailer, long StartXref)
{
    /// <summary>Gets the decoded object streams read so far, by object number.</summary>
    internal Dictionary<int, byte[]> ObjectStreams { get; } = [];
}
