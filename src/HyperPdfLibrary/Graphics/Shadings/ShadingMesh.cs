// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>The triangles of a mesh shading, ready to draw, with the colour ramp used when the shading has a function.</summary>
[DebuggerDisplay("ShadingMesh: {Chunks.Length} chunks")]
internal sealed class ShadingMesh
{
    /// <summary>Initializes a new instance of the <see cref="ShadingMesh"/> class.</summary>
    /// <param name="chunks">The vertex lists.</param>
    /// <param name="ramp">The shader giving colours from texture coordinates, or null when vertices carry colours.</param>
    internal ShadingMesh(SKVertices[] chunks, SKShader? ramp)
    {
        Chunks = chunks;
        Ramp = ramp;
    }

    /// <summary>Gets the vertex lists; each stays below the 16-bit index limit.</summary>
    internal SKVertices[] Chunks { get; }

    /// <summary>Gets the colour ramp shader, or null when vertices carry colours.</summary>
    internal SKShader? Ramp { get; }
}
