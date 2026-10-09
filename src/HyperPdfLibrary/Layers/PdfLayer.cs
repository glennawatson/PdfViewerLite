// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>A layer (optional content group).</summary>
/// <param name="Id">The group's object number.</param>
/// <param name="Name">The group's name.</param>
/// <param name="IsVisible">Whether it is shown.</param>
[DebuggerDisplay("PdfLayer: {Name} visible={IsVisible}")]
public readonly record struct PdfLayer(int Id, string Name, bool IsVisible);
