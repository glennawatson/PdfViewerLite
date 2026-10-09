// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Finds every glyph a set of TrueType glyphs needs: the glyphs, glyph 0 and the parts of composite glyphs.</summary>
internal static class GlyphClosure
{
    /// <summary>The bytes of a glyph header.</summary>
    private const int GlyphHeader = 10;

    /// <summary>The bytes of a component's flags and glyph index.</summary>
    private const int ComponentHeader = 4;

    /// <summary>The offset of a component's glyph index.</summary>
    private const int ComponentGlyph = 2;

    /// <summary>The composite flag: arguments are 16-bit.</summary>
    private const int ArgsAreWords = 0x0001;

    /// <summary>The composite flag: one scale follows.</summary>
    private const int HasScale = 0x0008;

    /// <summary>The composite flag: more components follow.</summary>
    private const int MoreComponents = 0x0020;

    /// <summary>The composite flag: separate x and y scales follow.</summary>
    private const int HasXyScale = 0x0040;

    /// <summary>The composite flag: a two by two matrix follows.</summary>
    private const int HasMatrix = 0x0080;

    /// <summary>The bytes of two 16-bit arguments.</summary>
    private const int WordArgs = 4;

    /// <summary>The bytes of two 8-bit arguments.</summary>
    private const int ByteArgs = 2;

    /// <summary>The bytes of one 2.14 scale.</summary>
    private const int ScaleBytes = 2;

    /// <summary>The bytes of two 2.14 scales.</summary>
    private const int XyScaleBytes = 4;

    /// <summary>The bytes of a 2.14 matrix.</summary>
    private const int MatrixBytes = 8;

    /// <summary>The most components read from one composite glyph.</summary>
    private const int MaxComponents = 256;

    /// <summary>Marks the glyphs to keep.</summary>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf, one more than the glyph count.</param>
    /// <param name="used">The glyphs the document shows.</param>
    /// <returns>Whether each glyph is kept.</returns>
    internal static bool[] Find(ReadOnlySpan<byte> glyf, int[] offsets, HashSet<int> used)
    {
        var keep = new bool[offsets.Length - 1];
        var pending = new Stack<int>();
        pending.Push(0);
        foreach (var glyph in used)
        {
            pending.Push(glyph);
        }

        while (pending.TryPop(out var glyph))
        {
            if ((uint)glyph >= (uint)keep.Length || keep[glyph])
            {
                continue;
            }

            keep[glyph] = true;
            AddComponents(glyf[offsets[glyph]..offsets[glyph + 1]], pending);
        }

        return keep;
    }

    /// <summary>Queues the components of a composite glyph.</summary>
    /// <param name="glyph">The glyph's data.</param>
    /// <param name="pending">The glyphs still to visit.</param>
    private static void AddComponents(ReadOnlySpan<byte> glyph, Stack<int> pending)
    {
        if (glyph.Length < GlyphHeader || BinaryPrimitives.ReadInt16BigEndian(glyph) >= 0)
        {
            return;
        }

        var position = GlyphHeader;
        for (var i = 0; i < MaxComponents && position + ComponentHeader <= glyph.Length; i++)
        {
            var flags = BinaryPrimitives.ReadUInt16BigEndian(glyph[position..]);
            pending.Push(BinaryPrimitives.ReadUInt16BigEndian(glyph[(position + ComponentGlyph)..]));
            position += ComponentHeader + ArgumentBytes(flags);
            if ((flags & MoreComponents) == 0)
            {
                return;
            }
        }
    }

    /// <summary>Gets the bytes of a component's arguments and transform.</summary>
    /// <param name="flags">The component's flags.</param>
    /// <returns>The bytes after its header.</returns>
    private static int ArgumentBytes(int flags)
    {
        var bytes = (flags & ArgsAreWords) != 0 ? WordArgs : ByteArgs;
        if ((flags & HasScale) != 0)
        {
            return bytes + ScaleBytes;
        }

        if ((flags & HasXyScale) != 0)
        {
            return bytes + XyScaleBytes;
        }

        return (flags & HasMatrix) != 0 ? bytes + MatrixBytes : bytes;
    }
}
