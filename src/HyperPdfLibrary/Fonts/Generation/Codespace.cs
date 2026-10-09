// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>A codespace range.</summary>
/// <param name="Length">The code length in bytes.</param>
/// <param name="Low">The first code.</param>
/// <param name="High">The last code.</param>
internal readonly record struct Codespace(int Length, uint Low, uint High);
