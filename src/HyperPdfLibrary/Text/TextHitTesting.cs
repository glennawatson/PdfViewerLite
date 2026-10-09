// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>Finds a character in original order, using contiguous block bounds to avoid unrelated boxes.</summary>
internal static class TextHitTesting
{
    /// <summary>Characters in one indexed block.</summary>
    private const int BlockSize = 32;

    /// <summary>Pages smaller than four blocks use a direct scan.</summary>
    private const int MinimumIndexedCharacters = 128;

    /// <summary>Half, for spreading tolerance across both sides of a box.</summary>
    private const float Half = 0.5F;

    /// <summary>The initial nearest distance on each axis, matching the PDFium search.</summary>
    private const double FarAway = 5000;

    /// <summary>Builds conservative bounds for contiguous groups of characters on larger pages.</summary>
    /// <param name="chars">The page characters in original order.</param>
    /// <returns>The block bounds, or an empty array for a small page.</returns>
    internal static TextHitBlock[] BuildBlocks(ReadOnlySpan<PdfTextChar> chars)
    {
        if (chars.Length < MinimumIndexedCharacters)
        {
            return [];
        }

        var blocks = new TextHitBlock[1 + ((chars.Length - 1) / BlockSize)];
        for (var blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            var start = blockIndex * BlockSize;
            var count = Math.Min(BlockSize, chars.Length - start);
            var first = chars[start].Box;
            var unbounded = !IsFinite(first);
            var bounds = unbounded ? default : TextGeometry.Normalize(first);
            for (var i = start + 1; i < start + count && !unbounded; i++)
            {
                var box = chars[i].Box;
                unbounded = !IsFinite(box);
                if (!unbounded)
                {
                    bounds = bounds.Union(TextGeometry.Normalize(box));
                }
            }

            // NaN and infinity can change Contains and distance arithmetic. Such a block must be scanned.
            if (unbounded)
            {
                bounds = new(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);
            }

            blocks[blockIndex] = new(bounds, start, count);
        }

        return blocks;
    }

    /// <summary>Finds the first exact hit, then the nearest expanded hit using the original strict tie rule.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="blocks">Bounds of contiguous character blocks, or empty on small pages.</param>
    /// <param name="point">The point in user space.</param>
    /// <param name="toleranceX">The horizontal tolerance.</param>
    /// <param name="toleranceY">The vertical tolerance.</param>
    /// <returns>The matching index, or -1.</returns>
    internal static int Find(ReadOnlySpan<PdfTextChar> chars, ReadOnlySpan<TextHitBlock> blocks, Vector2 point, float toleranceX, float toleranceY)
    {
        var query = new HitQuery(point, toleranceX, toleranceY, toleranceX > 0 || toleranceY > 0);
        var result = new HitResult(-1, FarAway, FarAway);
        if (!CanUseBlocks(blocks, query))
        {
            var exact = Scan(chars, 0, chars.Length, query, ref result);
            return exact >= 0 ? exact : result.Nearest;
        }

        foreach (var block in blocks)
        {
            var bounds = query.UseTolerance ? Expand(block.Bounds, toleranceX, toleranceY) : block.Bounds;
            if (!TextGeometry.Contains(bounds, point))
            {
                continue;
            }

            var exact = Scan(chars, block.Start, block.Count, query, ref result);
            if (exact >= 0)
            {
                return exact;
            }
        }

        return result.Nearest;
    }

    /// <summary>Scans one contiguous character range in original order.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="start">The first character index.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="query">The query values.</param>
    /// <param name="result">The best tolerance hit found so far.</param>
    /// <returns>The first exact hit, or -1.</returns>
    private static int Scan(ReadOnlySpan<PdfTextChar> chars, int start, int count, HitQuery query, ref HitResult result)
    {
        for (var i = start; i < start + count; i++)
        {
            var box = chars[i].Box;
            if (TextGeometry.Contains(box, query.Point))
            {
                return i;
            }

            box = TextGeometry.Normalize(box);
            if (!query.UseTolerance || !TextGeometry.Contains(Expand(box, query.ToleranceX, query.ToleranceY), query.Point))
            {
                continue;
            }

            double dx = MathF.Min(MathF.Abs(query.Point.X - box.Left), MathF.Abs(query.Point.X - box.Right));
            double dy = MathF.Min(MathF.Abs(query.Point.Y - box.Bottom), MathF.Abs(query.Point.Y - box.Top));
            if (dx + dy >= result.BestX + result.BestY)
            {
                continue;
            }

            result.BestX = dx;
            result.BestY = dy;
            result.Nearest = i;
        }

        return -1;
    }

    /// <summary>Grows a normalized box by half a tolerance on each side.</summary>
    /// <param name="box">The normalized box.</param>
    /// <param name="toleranceX">The horizontal tolerance.</param>
    /// <param name="toleranceY">The vertical tolerance.</param>
    /// <returns>The expanded box.</returns>
    private static PdfRectangle Expand(in PdfRectangle box, float toleranceX, float toleranceY)
    {
        var halfX = toleranceX * Half;
        var halfY = toleranceY * Half;
        return new(box.Left - halfX, box.Bottom - halfY, box.Right + halfX, box.Top + halfY);
    }

    /// <summary>Checks whether a block can be skipped safely for this query.</summary>
    /// <param name="blocks">The block summaries.</param>
    /// <param name="query">The query values.</param>
    /// <returns>Whether block bounds can filter this query.</returns>
    private static bool CanUseBlocks(ReadOnlySpan<TextHitBlock> blocks, HitQuery query) =>
        !blocks.IsEmpty && IsFinite(query.Point, query.ToleranceX, query.ToleranceY) && query.ToleranceX >= 0 && query.ToleranceY >= 0;

    /// <summary>Checks whether every box edge is finite.</summary>
    /// <param name="box">The box.</param>
    /// <returns>Whether all four edges are finite.</returns>
    private static bool IsFinite(in PdfRectangle box) =>
        float.IsFinite(box.Left) && float.IsFinite(box.Bottom) && float.IsFinite(box.Right) && float.IsFinite(box.Top);

    /// <summary>Checks whether a query has finite coordinates and tolerances.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="toleranceX">The horizontal tolerance.</param>
    /// <param name="toleranceY">The vertical tolerance.</param>
    /// <returns>Whether all query values are finite.</returns>
    private static bool IsFinite(Vector2 point, float toleranceX, float toleranceY) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(toleranceX) && float.IsFinite(toleranceY);

    /// <summary>The values shared by all character scans for one query.</summary>
    /// <param name="Point">The query point.</param>
    /// <param name="ToleranceX">The horizontal tolerance.</param>
    /// <param name="ToleranceY">The vertical tolerance.</param>
    /// <param name="UseTolerance">Whether either tolerance is positive.</param>
    private readonly record struct HitQuery(Vector2 Point, float ToleranceX, float ToleranceY, bool UseTolerance);

    /// <summary>The best tolerance hit found so far.</summary>
    /// <param name="Nearest">The nearest character index.</param>
    /// <param name="BestX">The distance from the nearest character's horizontal edge.</param>
    /// <param name="BestY">The distance from the nearest character's vertical edge.</param>
    private record struct HitResult(int Nearest, double BestX, double BestY);
}
