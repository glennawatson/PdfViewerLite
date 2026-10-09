// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>How <see cref="OptimizedFileWriter"/> lays out a file.</summary>
/// <param name="ObjectStreams">Whether to pack objects into object streams with a cross-reference stream.</param>
/// <param name="KeepEncryption">Whether to keep the document's encryption.</param>
/// <param name="Version">The header version, such as "1.7".</param>
[DebuggerDisplay("WriteSettings: {Version}, object streams {ObjectStreams}")]
internal readonly record struct WriteSettings(bool ObjectStreams, bool KeepEncryption, string Version);
