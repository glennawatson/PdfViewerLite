// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Media;

/// <summary>A sound object (a stream of samples).</summary>
/// <param name="Rate">The sampling rate in samples per second.</param>
/// <param name="Channels">The number of channels.</param>
/// <param name="BitsPerSample">The bits in each sample.</param>
/// <param name="Encoding">The sample encoding: Raw, Signed, muLaw or ALaw.</param>
/// <param name="Compression">The <c>/CO</c> compression format, or null.</param>
/// <param name="Data">The sound stream.</param>
[DebuggerDisplay("PdfSound: {Rate} Hz {Channels} channels")]
public sealed record PdfSound(double Rate, int Channels, int BitsPerSample, string Encoding, string? Compression, PdfStream Data);
