// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>What the header of a look-up table profile says about its colour spaces.</summary>
/// <param name="Components">The number of device channels.</param>
/// <param name="LabPcs">Whether the profile connection space is Lab rather than XYZ.</param>
/// <param name="LabInput">Whether the device colour space is Lab.</param>
/// <param name="Version">The major version of the profile.</param>
/// <param name="Darkest">The device values of the darkest colour, or <see langword="null"/> when the space has no known darkest colour.</param>
internal sealed record IccLutShape(int Components, bool LabPcs, bool LabInput, int Version, float[]? Darkest);
