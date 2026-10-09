// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>The Unicode properties text extraction needs: direction, mirroring and decomposition, matching PDFium's tables.</summary>
internal static partial class TextUnicode
{
    /// <summary>The longest decomposition in the table.</summary>
    internal const int MaxDecomposition = 18;

    /// <summary>The first Latin ligature, U+FB00 ff.</summary>
    private const char FirstLigature = (char)0xFB00;

    /// <summary>The last Latin ligature PDFium decomposes, U+FB06 st.</summary>
    private const char LastLigature = (char)0xFB06;

    /// <summary>Gets a character's direction.</summary>
    /// <param name="value">The character.</param>
    /// <returns>The direction.</returns>
    internal static TextDirection GetDirection(char value)
    {
        var index = DirectionStarts.BinarySearch((ushort)value);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return (TextDirection)DirectionKinds[index];
    }

    /// <summary>Gets a character's mirrored form, as right-to-left text shows brackets.</summary>
    /// <param name="value">The character.</param>
    /// <returns>The mirrored character, or the character itself.</returns>
    internal static char GetMirror(char value)
    {
        var index = MirrorKeys.BinarySearch((ushort)value);
        return index < 0 ? value : (char)MirrorValues[index];
    }

    /// <summary>Determines whether a character is a Latin ligature PDFium splits into letters.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for U+FB00 to U+FB06.</returns>
    internal static bool IsLigature(char value) => value is >= FirstLigature and <= LastLigature;

    /// <summary>Writes a character's decomposition, or the character itself when it has none.</summary>
    /// <param name="value">The character.</param>
    /// <param name="destination">The buffer, at least <see cref="MaxDecomposition"/> characters.</param>
    /// <returns>The characters written.</returns>
    internal static int Decompose(char value, Span<char> destination)
    {
        var index = DecompositionKeys.BinarySearch((ushort)value);
        if (index < 0)
        {
            destination[0] = value;
            return 1;
        }

        var offsets = DecompositionOffsets;
        var values = DecompositionValues[offsets[index]..offsets[index + 1]];
        for (var i = 0; i < values.Length; i++)
        {
            destination[i] = (char)values[i];
        }

        return values.Length;
    }

    /// <summary>Gets the overall direction of text: right to left when it has more right-to-left runs than left-to-right ones.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the text reads right to left.</returns>
    internal static bool IsRightToLeft(ReadOnlySpan<char> text)
    {
        var right = 0;
        var left = 0;
        var current = TextDirection.Neutral;
        var started = false;
        foreach (var value in text)
        {
            var direction = GetDirection(value);
            if (started && direction == current)
            {
                continue;
            }

            started = true;
            current = direction;
            right += direction == TextDirection.Right ? 1 : 0;
            left += direction == TextDirection.Left ? 1 : 0;
        }

        return right > left;
    }
}
