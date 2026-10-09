// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Three floats: a colour triple such as XYZ or RGB.</summary>
/// <param name="X">The first value.</param>
/// <param name="Y">The second value.</param>
/// <param name="Z">The third value.</param>
internal readonly record struct Float3(float X, float Y, float Z);
