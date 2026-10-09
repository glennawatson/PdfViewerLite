// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Styles drawn artificially when the system has no matching face.</summary>
/// <param name="Bold">Whether glyphs are emboldened.</param>
/// <param name="Slant">Whether glyphs are slanted.</param>
[DebuggerDisplay("SyntheticStyle: bold {Bold}, slant {Slant}")]
internal readonly record struct SyntheticStyle(bool Bold, bool Slant);
