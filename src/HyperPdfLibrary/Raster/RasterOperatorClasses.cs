// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;

namespace HyperPdfLibrary.Raster;

/// <summary>Maps content operators to their <see cref="RasterOperatorClass"/>.</summary>
internal static class RasterOperatorClasses
{
    /// <summary>The size of the lookup table; larger than any operator value.</summary>
    private const int TableSize = 128;

    /// <summary>The classes by operator value; operators not listed are neutral.</summary>
    private static readonly RasterOperatorClass[] Table = Build();

    /// <summary>Gets the class of an operator.</summary>
    /// <param name="op">The operator.</param>
    /// <returns>The class.</returns>
    internal static RasterOperatorClass Classify(ContentOperator op) =>
        (uint)op < (uint)Table.Length ? Table[(int)op] : RasterOperatorClass.Neutral;

    /// <summary>Builds the table.</summary>
    /// <returns>The table.</returns>
    private static RasterOperatorClass[] Build()
    {
        var table = new RasterOperatorClass[TableSize];
        table[(int)ContentOperator.Save] = RasterOperatorClass.Save;
        table[(int)ContentOperator.Restore] = RasterOperatorClass.Restore;
        table[(int)ContentOperator.ConcatMatrix] = RasterOperatorClass.Matrix;
        table[(int)ContentOperator.PaintXObject] = RasterOperatorClass.PaintXObject;
        table[(int)ContentOperator.BeginInlineImage] = RasterOperatorClass.InlineImage;
        table[(int)ContentOperator.SetRenderMode] = RasterOperatorClass.RenderMode;
        ReadOnlySpan<ContentOperator> vector =
        [
            ContentOperator.Fill, ContentOperator.FillEvenOdd, ContentOperator.Stroke, ContentOperator.CloseStroke,
            ContentOperator.FillStroke, ContentOperator.FillStrokeEvenOdd, ContentOperator.CloseFillStroke,
            ContentOperator.CloseFillStrokeEvenOdd, ContentOperator.PaintShading,
        ];
        foreach (var op in vector)
        {
            table[(int)op] = RasterOperatorClass.Vector;
        }

        ReadOnlySpan<ContentOperator> show =
        [
            ContentOperator.ShowText, ContentOperator.ShowTextArray, ContentOperator.NextLineShowText,
            ContentOperator.SetSpacingNextLineShowText,
        ];
        foreach (var op in show)
        {
            table[(int)op] = RasterOperatorClass.ShowText;
        }

        return table;
    }
}
