// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <content>Types 6 and 7: Coons and tensor-product patches.</content>
internal sealed partial class MeshBuilder
{
    /// <summary>The control points in a Coons patch.</summary>
    private const int CoonsPoints = 12;

    /// <summary>The control points in a tensor patch.</summary>
    private const int TensorPoints = 16;

    /// <summary>The control points an edge-sharing patch leaves to read from the stream.</summary>
    private const int SharedEdgePoints = 4;

    /// <summary>The corner colours in a patch.</summary>
    private const int CornerColors = 4;

    /// <summary>The corner colours an edge-sharing patch takes from its neighbour.</summary>
    private const int SharedColors = 2;

    /// <summary>The mask of the edge flag's two bits.</summary>
    private const int FlagMask = 3;

    /// <summary>The control points of the current patch, as p[i * 4 + j].</summary>
    private readonly PdfPoint[] _points = new PdfPoint[TensorPoints];

    /// <summary>The control points of the previous patch.</summary>
    private readonly PdfPoint[] _previousPoints = new PdfPoint[TensorPoints];

    /// <summary>The corner colours of the current patch: c00, c03, c33, c30.</summary>
    private readonly MeshVertex[] _corners = new MeshVertex[CornerColors];

    /// <summary>The corner colours of the previous patch.</summary>
    private readonly MeshVertex[] _previousCorners = new MeshVertex[CornerColors];

    /// <summary>Gets the grid index of each control point in stream order.</summary>
    private static ReadOnlySpan<byte> StreamOrder => [0x00, 0x01, 0x02, 0x03, 0x07, 0x0B, 0x0F, 0x0E, 0x0D, 0x0C, 0x08, 0x04, 0x05, 0x06, 0x0A, 0x09];

    /// <summary>Gets the previous patch's control points that start a patch with flag 1, 2 and 3, four per flag.</summary>
    private static ReadOnlySpan<byte> SharedPoints => [0x03, 0x07, 0x0B, 0x0F, 0x0F, 0x0E, 0x0D, 0x0C, 0x0C, 0x08, 0x04, 0x00];

    /// <summary>Gets the previous patch's corner colours that start a patch with flag 1, 2 and 3, two per flag.</summary>
    private static ReadOnlySpan<byte> SharedCorners => [0x01, 0x02, 0x02, 0x03, 0x03, 0x00];

    /// <summary>Reads the patches of a type 6 or 7 shading.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="tensor">Whether patches have 16 control points.</param>
    private void ReadPatches(ref MeshBitReader reader, bool tensor)
    {
        var havePrevious = false;
        while (reader.Remaining > _flagBits)
        {
            var flag = (int)reader.Read(_flagBits) & FlagMask;
            if ((flag != 0 && !havePrevious) || !ReadPatch(ref reader, flag, tensor))
            {
                return;
            }

            havePrevious = true;
        }
    }

    /// <summary>Reads one patch and adds its triangles.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="flag">The edge flag.</param>
    /// <param name="tensor">Whether the patch has 16 control points.</param>
    /// <returns><see langword="false"/> when the data ends inside the patch.</returns>
    private bool ReadPatch(ref MeshBitReader reader, int flag, bool tensor)
    {
        var pointCount = tensor ? TensorPoints : CoonsPoints;
        var firstPoint = flag == 0 ? 0 : SharedEdgePoints;
        var firstColor = flag == 0 ? 0 : SharedColors;
        var pointBits = (long)(pointCount - firstPoint) * PointFields * _coordinateBits;
        var colorBits = (long)(CornerColors - firstColor) * _componentCount * _componentBits;
        if (reader.Remaining < pointBits + colorBits)
        {
            return false;
        }

        if (flag != 0)
        {
            CopyShared(flag);
        }

        for (var k = firstPoint; k < pointCount; k++)
        {
            _points[StreamOrder[k]] = ReadPoint(ref reader);
        }

        for (var k = firstColor; k < CornerColors; k++)
        {
            _corners[k] = ReadColor(ref reader, default);
        }

        reader.Align();
        if (!tensor)
        {
            PatchTessellator.FillCoonsInterior(_points);
        }

        PatchTessellator.Emit(this, _points, _corners, _scale);
        _points.CopyTo(_previousPoints, 0);
        _corners.CopyTo(_previousCorners, 0);
        return true;
    }

    /// <summary>Starts a patch with the edge it shares with the previous one.</summary>
    /// <param name="flag">The edge flag, 1 to 3.</param>
    private void CopyShared(int flag)
    {
        var pointStart = (flag - 1) * SharedEdgePoints;
        for (var k = 0; k < SharedEdgePoints; k++)
        {
            _points[StreamOrder[k]] = _previousPoints[SharedPoints[pointStart + k]];
        }

        var colorStart = (flag - 1) * SharedColors;
        for (var k = 0; k < SharedColors; k++)
        {
            _corners[k] = _previousCorners[SharedCorners[colorStart + k]];
        }
    }
}
