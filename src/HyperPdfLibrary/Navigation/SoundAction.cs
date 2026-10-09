// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Media;

namespace HyperPdfLibrary.Navigation;

/// <summary>Plays a sound.</summary>
/// <param name="Sound">The sound, or null when the action names none.</param>
/// <param name="Volume">The volume from -1 to 1.</param>
/// <param name="Synchronous">Whether the viewer waits for the sound to finish.</param>
/// <param name="Repeat">Whether the sound repeats.</param>
/// <param name="Mix">Whether the sound mixes with others.</param>
[DebuggerDisplay("SoundAction: volume {Volume}")]
public sealed record SoundAction(PdfSound? Sound, double Volume, bool Synchronous, bool Repeat, bool Mix);
