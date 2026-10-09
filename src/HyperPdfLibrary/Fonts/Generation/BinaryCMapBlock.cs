// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>The shape of an encoded binary CMap block.</summary>
/// <param name="Kind">The mapping kind.</param>
/// <param name="Width">The encoded value width.</param>
/// <param name="Count">The entry count.</param>
/// <param name="Sequential">Whether source codes directly follow the previous range.</param>
internal readonly record struct BinaryCMapBlock(int Kind, int Width, int Count, bool Sequential);
