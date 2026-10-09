// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Reads the look-up table types of an ICC profile that convert device colours to the connection space: 'mft1' (lut8Type),
/// 'mft2' (lut16Type) and 'mAB ' (lutAToBType). Every length and offset is checked, and oversized tables are refused, so a
/// damaged profile gives <see langword="null"/> rather than an exception.
/// </summary>
internal static class IccLutParser
{
    /// <summary>The most nodes a look-up table may hold; a node takes 16 bytes.</summary>
    internal const int MaxClutNodes = 1 << 21;

    /// <summary>The 'mft1' type signature.</summary>
    private const uint Lut8Signature = 0x6D667431;

    /// <summary>The 'mft2' type signature.</summary>
    private const uint Lut16Signature = 0x6D667432;

    /// <summary>The 'mAB ' type signature.</summary>
    private const uint LutAToBSignature = 0x6D414220;

    /// <summary>The offset of the input channel count.</summary>
    private const int InputChannelsOffset = 8;

    /// <summary>The offset of the output channel count.</summary>
    private const int OutputChannelsOffset = 9;

    /// <summary>The offset of the grid point count in an 'mft1' or 'mft2' tag.</summary>
    private const int GridPointsOffset = 10;

    /// <summary>The offset of the input table entry count in an 'mft2' tag.</summary>
    private const int InputEntriesOffset = 48;

    /// <summary>The offset of the output table entry count in an 'mft2' tag.</summary>
    private const int OutputEntriesOffset = 50;

    /// <summary>The offset of the tables in an 'mft1' tag.</summary>
    private const int Lut8DataOffset = 48;

    /// <summary>The offset of the tables in an 'mft2' tag.</summary>
    private const int Lut16DataOffset = 52;

    /// <summary>The entries of every table in an 'mft1' tag.</summary>
    private const int Lut8Entries = 256;

    /// <summary>The most entries a table in an 'mft2' tag may have.</summary>
    private const int MaxLut16Entries = 4096;

    /// <summary>The fewest entries a table may have.</summary>
    private const int MinEntries = 2;

    /// <summary>The output channels of a pipeline to the connection space.</summary>
    private const int OutputChannels = 3;

    /// <summary>The bytes in an 'mAB ' tag before its offsets end.</summary>
    private const int MabHeaderSize = 32;

    /// <summary>The offset of the output curves in an 'mAB ' tag.</summary>
    private const int MabOutputCurvesOffset = 12;

    /// <summary>The offset of the matrix in an 'mAB ' tag.</summary>
    private const int MabMatrixOffset = 16;

    /// <summary>The offset of the matrix curves in an 'mAB ' tag.</summary>
    private const int MabMatrixCurvesOffset = 20;

    /// <summary>The offset of the look-up table in an 'mAB ' tag.</summary>
    private const int MabClutOffset = 24;

    /// <summary>The offset of the input curves in an 'mAB ' tag.</summary>
    private const int MabInputCurvesOffset = 28;

    /// <summary>The bytes of grid point counts at the start of an 'mAB ' look-up table.</summary>
    private const int MabGridBytes = 16;

    /// <summary>The bytes of an 'mAB ' look-up table before its samples.</summary>
    private const int MabClutHeaderSize = 20;

    /// <summary>The scale of an s15Fixed16 number.</summary>
    private const float FixedScale = 65536F;

    /// <summary>The bytes in an s15Fixed16 number.</summary>
    private const int FixedSize = 4;

    /// <summary>The largest 8-bit sample.</summary>
    private const float Maximum8 = 255F;

    /// <summary>The largest 16-bit sample.</summary>
    private const float Maximum16 = 65535F;

    /// <summary>The bytes in a 16-bit sample.</summary>
    private const int WideBytes = 2;

    /// <summary>Reads a device to connection space tag.</summary>
    /// <param name="tag">The tag data, starting at its type signature.</param>
    /// <param name="inputs">The number of device channels the profile's colour space has.</param>
    /// <param name="labPcs">Whether the profile connection space is Lab rather than XYZ.</param>
    /// <returns>The pipeline, or <see langword="null"/> when the tag is not a supported, valid table.</returns>
    internal static IccPipeline? Parse(ReadOnlySpan<byte> tag, int inputs, bool labPcs)
    {
        if (tag.Length < InputChannelsOffset + FixedSize || inputs is < 1 or > IccClut.MaxInputs)
        {
            return null;
        }

        return BinaryPrimitives.ReadUInt32BigEndian(tag) switch
        {
            Lut8Signature => ParseLut(tag, inputs, wide: false, labPcs ? IccPcsEncoding.Lab : IccPcsEncoding.Xyz),
            Lut16Signature => ParseLut(tag, inputs, wide: true, labPcs ? IccPcsEncoding.LabLegacy16 : IccPcsEncoding.Xyz),
            LutAToBSignature => ParseAToB(tag, inputs, labPcs ? IccPcsEncoding.Lab : IccPcsEncoding.Xyz),
            _ => null,
        };
    }

    /// <summary>Counts the nodes of a table.</summary>
    /// <param name="sizes">The nodes along each channel.</param>
    /// <returns>The node count, or -1 when it is above <see cref="MaxClutNodes"/>.</returns>
    private static long CountNodes(ReadOnlySpan<int> sizes)
    {
        long nodes = 1;
        foreach (var size in sizes)
        {
            nodes *= size;
            if (nodes > MaxClutNodes)
            {
                return -1;
            }
        }

        return nodes;
    }

    /// <summary>Checks the channel counts and grid size in the header of an 'mft1' or 'mft2' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="inputs">The expected input channels.</param>
    /// <param name="dataOffset">The offset where the tag's tables start; the tag must reach it.</param>
    /// <returns><see langword="true"/> when the header fits the expected profile.</returns>
    private static bool IsLutHeader(ReadOnlySpan<byte> tag, int inputs, int dataOffset) =>
        tag.Length >= dataOffset && tag[InputChannelsOffset] == inputs && tag[OutputChannelsOffset] == OutputChannels && tag[GridPointsOffset] >= MinEntries;

    /// <summary>Determines whether a table has a usable number of entries.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns><see langword="true"/> when there are between two and the most allowed.</returns>
    private static bool IsTableSize(int entries) => entries is >= MinEntries and <= MaxLut16Entries;

    /// <summary>Reads samples as values from 0 to 1.</summary>
    /// <param name="data">The data holding the samples.</param>
    /// <param name="count">The number of samples; the data holds at least that many.</param>
    /// <param name="wide">Whether samples are 16 bits rather than 8.</param>
    /// <returns>The values.</returns>
    private static float[] ReadSamples(ReadOnlySpan<byte> data, int count, bool wide)
    {
        var values = new float[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = wide ? BinaryPrimitives.ReadUInt16BigEndian(data[(i * WideBytes)..]) / Maximum16 : data[i] / Maximum8;
        }

        return values;
    }

    /// <summary>Builds one sampled curve per channel from consecutive tables.</summary>
    /// <param name="data">The data holding the tables.</param>
    /// <param name="channels">The number of tables.</param>
    /// <param name="entries">The entries of each table.</param>
    /// <param name="wide">Whether samples are 16 bits rather than 8.</param>
    /// <returns>The curves, or <see langword="null"/> when a table is too short.</returns>
    private static IccCurve[]? ReadTables(ReadOnlySpan<byte> data, int channels, int entries, bool wide)
    {
        var curves = new IccCurve[channels];
        var bytes = wide ? entries * WideBytes : entries;
        for (var c = 0; c < channels; c++)
        {
            var curve = IccCurve.FromTable(ReadSamples(data[(c * bytes)..], entries, wide));
            if (curve is null)
            {
                return null;
            }

            curves[c] = curve;
        }

        return curves;
    }

    /// <summary>Builds the nodes of a table whose output has three channels.</summary>
    /// <param name="data">The data holding the samples.</param>
    /// <param name="nodeCount">The number of nodes.</param>
    /// <param name="wide">Whether samples are 16 bits rather than 8.</param>
    /// <returns>The nodes.</returns>
    private static Vector128<float>[] ReadNodes(ReadOnlySpan<byte> data, int nodeCount, bool wide)
    {
        var samples = ReadSamples(data, nodeCount * OutputChannels, wide);
        var nodes = new Vector128<float>[nodeCount];
        for (var i = 0; i < nodes.Length; i++)
        {
            var s = i * OutputChannels;
            nodes[i] = Vector128.Create(samples[s], samples[s + 1], samples[s + OutputChannels - 1], 0F);
        }

        return nodes;
    }

    /// <summary>Checks the sizes of an 'mft1' or 'mft2' tag and works out where its parts lie.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="inputs">The expected input channels.</param>
    /// <param name="wide">Whether this is an 'mft2' tag.</param>
    /// <returns>The layout, or <see langword="null"/> when the tag is malformed or too large.</returns>
    private static LutLayout? ReadLutLayout(ReadOnlySpan<byte> tag, int inputs, bool wide)
    {
        var dataOffset = wide ? Lut16DataOffset : Lut8DataOffset;
        if (!IsLutHeader(tag, inputs, dataOffset))
        {
            return null;
        }

        var inputEntries = wide ? BinaryPrimitives.ReadUInt16BigEndian(tag[InputEntriesOffset..]) : Lut8Entries;
        var outputEntries = wide ? BinaryPrimitives.ReadUInt16BigEndian(tag[OutputEntriesOffset..]) : Lut8Entries;
        Span<int> sizes = stackalloc int[IccClut.MaxInputs];
        sizes = sizes[..inputs];
        sizes.Fill(tag[GridPointsOffset]);
        var nodes = CountNodes(sizes);
        if (nodes < 0 || !IsTableSize(inputEntries) || !IsTableSize(outputEntries))
        {
            return null;
        }

        var sampleBytes = wide ? WideBytes : 1;
        var inputBytes = (long)inputs * inputEntries * sampleBytes;
        var clutBytes = nodes * OutputChannels * sampleBytes;
        var outputBytes = (long)OutputChannels * outputEntries * sampleBytes;
        return dataOffset + inputBytes + clutBytes + outputBytes > tag.Length
            ? null
            : new(tag[GridPointsOffset], inputEntries, outputEntries, (int)nodes, dataOffset, (int)inputBytes, (int)clutBytes);
    }

    /// <summary>Reads an 'mft1' or 'mft2' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="inputs">The expected input channels.</param>
    /// <param name="wide">Whether this is an 'mft2' tag.</param>
    /// <param name="encoding">How the output is stored.</param>
    /// <returns>The pipeline, or <see langword="null"/> when invalid.</returns>
    private static IccPipeline? ParseLut(ReadOnlySpan<byte> tag, int inputs, bool wide, IccPcsEncoding encoding)
    {
        if (ReadLutLayout(tag, inputs, wide) is not { } layout)
        {
            return null;
        }

        var data = tag[layout.DataOffset..];
        var inputCurves = ReadTables(data, inputs, layout.InputEntries, wide);
        var outputCurves = ReadTables(data[(layout.InputBytes + layout.ClutBytes)..], OutputChannels, layout.OutputEntries, wide);
        if (inputCurves is null || outputCurves is null)
        {
            return null;
        }

        Span<int> sizes = stackalloc int[IccClut.MaxInputs];
        sizes = sizes[..inputs];
        sizes.Fill(layout.Grid);
        return new(inputs, inputCurves, new(sizes, ReadNodes(data[layout.InputBytes..], layout.Nodes, wide)), null, null, outputCurves, encoding);
    }

    /// <summary>Reads curves stored back to back at an offset given in the tag header.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="field">The offset of the header field that holds the curves' offset.</param>
    /// <param name="count">The number of curves.</param>
    /// <param name="curves">Receives the curves, or <see langword="null"/> when the field is zero.</param>
    /// <returns><see langword="false"/> when the field is set but the curves are invalid.</returns>
    private static bool TryReadCurves(ReadOnlySpan<byte> tag, int field, int count, out IccCurve[]? curves)
    {
        curves = null;
        var offset = BinaryPrimitives.ReadUInt32BigEndian(tag[field..]);
        if (offset is 0)
        {
            return true;
        }

        if (offset >= tag.Length)
        {
            return false;
        }

        var list = new IccCurve[count];
        var position = (int)offset;
        for (var i = 0; i < count; i++)
        {
            var length = position > tag.Length ? 0 : IccCurve.Measure(tag[position..]);
            var curve = length is 0 ? null : IccCurve.Parse(tag[position..]);
            if (curve is null)
            {
                return false;
            }

            list[i] = curve;
            position += length;
        }

        curves = list;
        return true;
    }

    /// <summary>Reads the three sets of curves of an 'mAB ' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="inputs">The input channels.</param>
    /// <param name="inputCurves">Receives the input curves, or <see langword="null"/> when absent.</param>
    /// <param name="matrixCurves">Receives the curves applied before the matrix, or <see langword="null"/> when absent.</param>
    /// <param name="outputCurves">Receives the output curves, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="false"/> when a set is present but invalid.</returns>
    private static bool TryReadCurveStages(ReadOnlySpan<byte> tag, int inputs, out IccCurve[]? inputCurves, out IccCurve[]? matrixCurves, out IccCurve[]? outputCurves)
    {
        matrixCurves = null;
        outputCurves = null;
        return TryReadCurves(tag, MabInputCurvesOffset, inputs, out inputCurves)
            && TryReadCurves(tag, MabMatrixCurvesOffset, OutputChannels, out matrixCurves)
            && TryReadCurves(tag, MabOutputCurvesOffset, OutputChannels, out outputCurves);
    }

    /// <summary>Reads the 3 by 4 matrix of an 'mAB ' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="matrix">Receives the matrix, or <see langword="null"/> when the header field is zero.</param>
    /// <returns><see langword="false"/> when the field is set but the matrix is cut short.</returns>
    private static bool TryReadMatrix(ReadOnlySpan<byte> tag, out float[]? matrix)
    {
        matrix = null;
        var offset = BinaryPrimitives.ReadUInt32BigEndian(tag[MabMatrixOffset..]);
        if (offset is 0)
        {
            return true;
        }

        if ((long)offset + (IccPipeline.MatrixSize * FixedSize) > tag.Length)
        {
            return false;
        }

        var values = new float[IccPipeline.MatrixSize];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = BinaryPrimitives.ReadInt32BigEndian(tag[((int)offset + (i * FixedSize))..]) / FixedScale;
        }

        matrix = values;
        return true;
    }

    /// <summary>Reads the grid sizes of an 'mAB ' look-up table.</summary>
    /// <param name="clut">The table data, starting at its grid point counts.</param>
    /// <param name="sizes">Receives the nodes along each input channel.</param>
    /// <returns><see langword="false"/> when a channel has fewer than two nodes.</returns>
    private static bool TryReadSizes(ReadOnlySpan<byte> clut, Span<int> sizes)
    {
        for (var i = 0; i < sizes.Length; i++)
        {
            sizes[i] = clut[i];
            if (sizes[i] < MinEntries)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the look-up table of an 'mAB ' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="inputs">The input channels.</param>
    /// <param name="clut">Receives the table, or <see langword="null"/> when the header field is zero.</param>
    /// <returns><see langword="false"/> when the field is set but the table is invalid or too large.</returns>
    private static bool TryReadClut(ReadOnlySpan<byte> tag, int inputs, out IccClut? clut)
    {
        clut = null;
        var offset = BinaryPrimitives.ReadUInt32BigEndian(tag[MabClutOffset..]);
        if (offset is 0)
        {
            return true;
        }

        if ((long)offset + MabClutHeaderSize > tag.Length)
        {
            return false;
        }

        var data = tag[(int)offset..];
        Span<int> sizes = stackalloc int[IccClut.MaxInputs];
        sizes = sizes[..inputs];
        var precision = data[MabGridBytes];
        if (!TryReadSizes(data, sizes) || precision is not (1 or WideBytes))
        {
            return false;
        }

        var nodes = CountNodes(sizes);
        if (nodes < 0 || nodes * OutputChannels * precision > data.Length - MabClutHeaderSize)
        {
            return false;
        }

        clut = new(sizes, ReadNodes(data[MabClutHeaderSize..], (int)nodes, precision == WideBytes));
        return true;
    }

    /// <summary>Reads an 'mAB ' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <param name="inputs">The expected input channels.</param>
    /// <param name="encoding">How the output is stored.</param>
    /// <returns>The pipeline, or <see langword="null"/> when invalid.</returns>
    private static IccPipeline? ParseAToB(ReadOnlySpan<byte> tag, int inputs, IccPcsEncoding encoding)
    {
        if (tag.Length < MabHeaderSize || tag[InputChannelsOffset] != inputs || tag[OutputChannelsOffset] != OutputChannels)
        {
            return null;
        }

        // A stage whose offset is set but cannot be read invalidates the tag.
        if (!(TryReadCurveStages(tag, inputs, out var inputCurves, out var matrixCurves, out var outputCurves)
            && TryReadMatrix(tag, out var matrix)
            && TryReadClut(tag, inputs, out var clut)))
        {
            return null;
        }

        // Without a table the stages cannot change the channel count, and the output curves are required.
        return outputCurves is not null && (clut is not null || inputs == OutputChannels)
            ? new(inputs, inputCurves, clut, matrixCurves, matrix, outputCurves, encoding)
            : null;
    }

    /// <summary>Where the parts of an 'mft1' or 'mft2' tag lie.</summary>
    /// <param name="Grid">The nodes along each input channel.</param>
    /// <param name="InputEntries">The entries of each input table.</param>
    /// <param name="OutputEntries">The entries of each output table.</param>
    /// <param name="Nodes">The nodes of the table.</param>
    /// <param name="DataOffset">The offset of the input tables.</param>
    /// <param name="InputBytes">The bytes of the input tables.</param>
    /// <param name="ClutBytes">The bytes of the look-up table.</param>
    private readonly record struct LutLayout(int Grid, int InputEntries, int OutputEntries, int Nodes, int DataOffset, int InputBytes, int ClutBytes);
}
