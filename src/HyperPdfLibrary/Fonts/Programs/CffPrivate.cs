// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The parts of a CFF Private DICT the charstring interpreter needs.</summary>
/// <param name="Subrs">The local subroutines.</param>
/// <param name="DefaultWidth">The width of glyphs that give none.</param>
/// <param name="NominalWidth">The width added to explicit widths.</param>
[DebuggerDisplay("CffPrivate: {Subrs.Count} subrs")]
internal readonly record struct CffPrivate(CffIndex Subrs, float DefaultWidth, float NominalWidth)
{
    /// <summary>The Subrs operator.</summary>
    private const int SubrsOperator = 19;

    /// <summary>The defaultWidthX operator.</summary>
    private const int DefaultWidthOperator = 20;

    /// <summary>The nominalWidthX operator.</summary>
    private const int NominalWidthOperator = 21;

    /// <summary>Reads a Private DICT.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="size">The DICT's size.</param>
    /// <param name="offset">The DICT's offset.</param>
    /// <returns>The values; defaults when the DICT is missing.</returns>
    internal static CffPrivate Read(ReadOnlySpan<byte> data, int size, int offset)
    {
        var dict = FontBytes.Slice(data, offset, size);
        var reader = new CffDictReader(dict, stackalloc double[CharstringLimits.StackDepth]);
        var subrsOffset = -1;
        var defaultWidth = 0F;
        var nominalWidth = 0F;
        while (reader.TryRead(out var op))
        {
            switch (op)
            {
                case SubrsOperator:
                {
                    subrsOffset = reader.GetInt(0);
                    break;
                }

                case DefaultWidthOperator:
                {
                    defaultWidth = (float)reader.Get(0);
                    break;
                }

                case NominalWidthOperator:
                {
                    nominalWidth = (float)reader.Get(0);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        var subrs = subrsOffset > 0 && !dict.IsEmpty ? CffIndex.Read(data, offset + subrsOffset, out _) : default;
        return new(subrs, defaultWidth, nominalWidth);
    }
}
