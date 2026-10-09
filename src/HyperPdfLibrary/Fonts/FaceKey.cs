// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Identifies a cached system font: a family in a weight and slant.</summary>
/// <param name="Family">The family name asked for.</param>
/// <param name="Weight">The weight, 100 to 900.</param>
/// <param name="Italic">Whether the face slants.</param>
/// <param name="Requested">Whether the family is the one the PDF named rather than a generic stand-in.</param>
[DebuggerDisplay("FaceKey: {Family} {Weight} {Italic}")]
internal readonly record struct FaceKey(string Family, int Weight, bool Italic, bool Requested);
