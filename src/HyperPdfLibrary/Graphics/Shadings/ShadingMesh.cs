// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>The triangles of a mesh shading, ready to draw, with the colour ramp used when the shading has a function.</summary>
[DebuggerDisplay("ShadingMesh: {Chunks.Length} chunks")]
public sealed class ShadingMesh
{
    /// <summary>Initializes a new instance of the <see cref="ShadingMesh"/> class.</summary>
    /// <param name="chunks">The vertex lists.</param>
    /// <param name="ramp">The shader giving colours from texture coordinates, or null when vertices carry colours.</param>
    public ShadingMesh(IPdfRenderVertices[] chunks, IPdfRenderShader? ramp)
    {
        Chunks = chunks;
        Ramp = ramp;
    }

    /// <summary>Gets the vertex lists; each stays below the 16-bit index limit.</summary>
    public IPdfRenderVertices[] Chunks { get; }

    /// <summary>Gets the colour ramp shader, or null when vertices carry colours.</summary>
    public IPdfRenderShader? Ramp { get; }
}
