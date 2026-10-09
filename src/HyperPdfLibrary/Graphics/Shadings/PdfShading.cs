// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>
/// A parsed shading dictionary. Gradient and function shadings build one immutable <see cref="SKShader"/>; mesh shadings
/// build triangles. Both are made on first use and shared by every thread.
/// </summary>
[DebuggerDisplay("PdfShading: {Kind}")]
internal sealed class PdfShading
{
    /// <summary>The smallest power-of-two scale a patch mesh is cut for.</summary>
    private const int MinScaleExponent = -6;

    /// <summary>The largest power-of-two scale a patch mesh is cut for.</summary>
    private const int MaxScaleExponent = 8;

    /// <summary>The ratio between neighbouring scale buckets.</summary>
    private const float ScaleBase = 2;

    /// <summary>The number of scale buckets.</summary>
    private const int ScaleBuckets = MaxScaleExponent - MinScaleExponent + 1;

    /// <summary>Guards building the Skia objects.</summary>
    private readonly Lock _gate = new();

    /// <summary>Whether the shader has been built.</summary>
    private bool _shaderBuilt;

    /// <summary>The built shader, or null when the shading cannot be drawn with one.</summary>
    private SKShader? _shader;

    /// <summary>Whether the mesh of each scale bucket has been built.</summary>
    private bool[]? _meshBuilt;

    /// <summary>The built meshes by scale bucket.</summary>
    private ShadingMesh?[]? _meshes;

    /// <summary>Initializes a new instance of the <see cref="PdfShading"/> class.</summary>
    /// <param name="kind">The shading type.</param>
    /// <param name="dictionary">The shading dictionary.</param>
    /// <param name="stream">The shading stream for mesh types, otherwise null.</param>
    /// <param name="colorSpace">The colour space.</param>
    /// <param name="function">The colour function, or null.</param>
    private PdfShading(PdfShadingKind kind, PdfDictionary dictionary, PdfStream? stream, PdfColorSpace colorSpace, PdfFunction? function)
    {
        Kind = kind;
        Dictionary = dictionary;
        Stream = stream;
        ColorSpace = colorSpace;
        Function = function;
        BBox = dictionary.TryGetRectangle(KnownName.BBox, out var box) ? box : null;
        Background = ReadBackground(dictionary, colorSpace);
    }

    /// <summary>Gets the shading type.</summary>
    internal PdfShadingKind Kind { get; }

    /// <summary>Gets the shading dictionary.</summary>
    internal PdfDictionary Dictionary { get; }

    /// <summary>Gets the shading stream for mesh types, otherwise null.</summary>
    internal PdfStream? Stream { get; }

    /// <summary>Gets the colour space.</summary>
    internal PdfColorSpace ColorSpace { get; }

    /// <summary>Gets the colour function, or null when colours are given directly (meshes only).</summary>
    internal PdfFunction? Function { get; }

    /// <summary>Gets the bounding box in shading space, or null.</summary>
    internal PdfRectangle? BBox { get; }

    /// <summary>Gets the background colour as 0xRRGGBB, or null.</summary>
    internal uint? Background { get; }

    /// <summary>Gets a value indicating whether the shading is drawn as triangles.</summary>
    internal bool IsMesh => Kind >= PdfShadingKind.FreeForm;

    /// <summary>Parses a shading.</summary>
    /// <param name="value">The shading dictionary or stream, resolved.</param>
    /// <param name="colorSpaceResources">The /ColorSpace resource dictionary for named spaces, or null.</param>
    /// <returns>The shading, or null when it is damaged.</returns>
    internal static PdfShading? Parse(PdfValue value, PdfDictionary? colorSpaceResources)
    {
        var dictionary = value.AsDictionary();
        if (dictionary is null)
        {
            return null;
        }

        var type = dictionary.GetInt32(KnownName.ShadingType);
        if (type is < (int)PdfShadingKind.FunctionBased or > (int)PdfShadingKind.Tensor)
        {
            return null;
        }

        var space = PdfColorSpace.Parse(dictionary.Get(KnownName.ColorSpace), colorSpaceResources);
        var functionValue = dictionary.Get(KnownName.Function);
        var function = functionValue.IsNull ? null : PdfFunction.Parse(functionValue);
        var kind = (PdfShadingKind)type;
        var stream = value.AsStream();
        return kind >= PdfShadingKind.FreeForm && stream is null ? null : new(kind, dictionary, stream, space, function);
    }

    /// <summary>Converts a colour to a Skia colour.</summary>
    /// <param name="space">The colour space.</param>
    /// <param name="components">The components.</param>
    /// <returns>The opaque colour.</returns>
    internal static SKColor ToSkColor(PdfColorSpace space, ReadOnlySpan<float> components)
    {
        var colour = ColorState.Resolve(space, components);
        return new(colour.Red, colour.Green, colour.Blue);
    }

    /// <summary>Gets the shader for gradient and function shadings, building it on first use.</summary>
    /// <returns>The shader, or null when it cannot be built or the shading is a mesh.</returns>
    internal SKShader? GetShader()
    {
        lock (_gate)
        {
            if (!_shaderBuilt)
            {
                _shader = IsMesh ? null : ShadingShaders.Create(this);
                _shaderBuilt = true;
            }

            return _shader;
        }
    }

    /// <summary>
    /// Gets the triangles for mesh shadings, building them on first use. Patch meshes are cut more finely the larger
    /// they are drawn, so they are kept per power-of-two scale; triangle meshes do not depend on the scale.
    /// </summary>
    /// <param name="scale">The device units one unit of shading space spans when drawn.</param>
    /// <returns>The mesh, or null when it is empty or the shading is not a mesh.</returns>
    internal ShadingMesh? GetMesh(float scale)
    {
        var bucket = Kind >= PdfShadingKind.Coons ? ScaleBucket(scale) : 0;
        lock (_gate)
        {
            _meshes ??= new ShadingMesh?[ScaleBuckets];
            _meshBuilt ??= new bool[ScaleBuckets];
            if (!_meshBuilt[bucket])
            {
                _meshes[bucket] = IsMesh ? MeshBuilder.Build(this, MathF.Pow(ScaleBase, bucket + MinScaleExponent)) : null;
                _meshBuilt[bucket] = true;
            }

            return _meshes[bucket];
        }
    }

    /// <summary>Gets the bucket of a scale: the power of two at or above it, clamped to the kept range.</summary>
    /// <param name="scale">The scale.</param>
    /// <returns>The bucket index.</returns>
    private static int ScaleBucket(float scale)
    {
        var exponent = scale > 0 && float.IsFinite(scale) ? (int)MathF.Ceiling(MathF.Log2(scale)) : 0;
        return Math.Clamp(exponent, MinScaleExponent, MaxScaleExponent) - MinScaleExponent;
    }

    /// <summary>Reads the /Background colour.</summary>
    /// <param name="dictionary">The shading dictionary.</param>
    /// <param name="space">The colour space.</param>
    /// <returns>The colour, or null.</returns>
    private static uint? ReadBackground(PdfDictionary dictionary, PdfColorSpace space)
    {
        var array = dictionary.GetArray(KnownName.Background);
        if (array is null || array.Count < space.Components || space.Components > PdfColorSpace.MaxComponents)
        {
            return null;
        }

        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        _ = array.ReadNumbers(components);
        return ColorState.Resolve(space, components).Rgb;
    }
}
