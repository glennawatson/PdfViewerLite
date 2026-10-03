// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Speech;

/// <summary>A file the on-device voice needs, downloaded once.</summary>
/// <param name="Source">Where it is downloaded from.</param>
/// <param name="LocalName">Its path inside the voice folder.</param>
/// <param name="ApproximateBytes">About how large it is, for the download message.</param>
[DebuggerDisplay("{LocalName}")]
public sealed record SpeechModelFile(Uri Source, string LocalName, long ApproximateBytes);
