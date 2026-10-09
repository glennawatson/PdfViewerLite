// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>One open marked content level.</summary>
/// <param name="Mcid">Its marked content id, or -1.</param>
/// <param name="IsArtifact">Whether its tag is <c>/Artifact</c>.</param>
[DebuggerDisplay("MarkFrame: mcid {Mcid} artifact={IsArtifact}")]
internal readonly record struct MarkFrame(int Mcid, bool IsArtifact);
