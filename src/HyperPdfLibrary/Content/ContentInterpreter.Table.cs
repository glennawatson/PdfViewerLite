// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Content;

/// <content>The operator dispatch table.</content>
internal sealed unsafe partial class ContentInterpreter
{
    /// <summary>The handlers, indexed by <see cref="ContentOperator"/>; operators the interpreter ignores point at a no-op.</summary>
    private static readonly delegate*<ContentInterpreter, ref ContentReader, void>[] Handlers = CreateHandlers();

    /// <summary>Builds the dispatch table.</summary>
    /// <returns>The table.</returns>
    private static delegate*<ContentInterpreter, ref ContentReader, void>[] CreateHandlers()
    {
        var table = new delegate*<ContentInterpreter, ref ContentReader, void>[Enum.GetValues<ContentOperator>().Length];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = &OpIgnore;
        }

        RegisterPaths(table);
        RegisterState(table);
        RegisterColors(table);
        RegisterText(table);
        RegisterOther(table);
        return table;
    }

    /// <summary>Registers path construction, painting and clipping.</summary>
    /// <param name="table">The table.</param>
    private static void RegisterPaths(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.MoveTo] = &OpMoveTo;
        table[(int)ContentOperator.LineTo] = &OpLineTo;
        table[(int)ContentOperator.CurveTo] = &OpCurveTo;
        table[(int)ContentOperator.CurveToV] = &OpCurveToV;
        table[(int)ContentOperator.CurveToY] = &OpCurveToY;
        table[(int)ContentOperator.ClosePath] = &OpClosePath;
        table[(int)ContentOperator.Rectangle] = &OpRectangle;
        table[(int)ContentOperator.Fill] = &OpFill;
        table[(int)ContentOperator.FillEvenOdd] = &OpFillEvenOdd;
        table[(int)ContentOperator.Stroke] = &OpStroke;
        table[(int)ContentOperator.CloseStroke] = &OpCloseStroke;
        table[(int)ContentOperator.FillStroke] = &OpFillStroke;
        table[(int)ContentOperator.FillStrokeEvenOdd] = &OpFillStrokeEvenOdd;
        table[(int)ContentOperator.CloseFillStroke] = &OpCloseFillStroke;
        table[(int)ContentOperator.CloseFillStrokeEvenOdd] = &OpCloseFillStrokeEvenOdd;
        table[(int)ContentOperator.EndPath] = &OpEndPath;
        table[(int)ContentOperator.Clip] = &OpClip;
        table[(int)ContentOperator.ClipEvenOdd] = &OpClipEvenOdd;
    }

    /// <summary>Registers the graphics state operators.</summary>
    /// <param name="table">The table.</param>
    private static void RegisterState(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.Save] = &OpSave;
        table[(int)ContentOperator.Restore] = &OpRestore;
        table[(int)ContentOperator.ConcatMatrix] = &OpConcatMatrix;
        table[(int)ContentOperator.SetLineWidth] = &OpSetLineWidth;
        table[(int)ContentOperator.SetLineCap] = &OpSetLineCap;
        table[(int)ContentOperator.SetLineJoin] = &OpSetLineJoin;
        table[(int)ContentOperator.SetMiterLimit] = &OpSetMiterLimit;
        table[(int)ContentOperator.SetDash] = &OpSetDash;
        table[(int)ContentOperator.SetGraphicsState] = &OpSetGraphicsState;
    }

    /// <summary>Registers the colour operators.</summary>
    /// <param name="table">The table.</param>
    private static void RegisterColors(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.SetFillGray] = &OpSetFillGray;
        table[(int)ContentOperator.SetStrokeGray] = &OpSetStrokeGray;
        table[(int)ContentOperator.SetFillRgb] = &OpSetFillRgb;
        table[(int)ContentOperator.SetStrokeRgb] = &OpSetStrokeRgb;
        table[(int)ContentOperator.SetFillCmyk] = &OpSetFillCmyk;
        table[(int)ContentOperator.SetStrokeCmyk] = &OpSetStrokeCmyk;
        table[(int)ContentOperator.SetFillColorSpace] = &OpSetFillColorSpace;
        table[(int)ContentOperator.SetStrokeColorSpace] = &OpSetStrokeColorSpace;
        table[(int)ContentOperator.SetFillColor] = &OpSetFillColor;
        table[(int)ContentOperator.SetFillColorN] = &OpSetFillColor;
        table[(int)ContentOperator.SetStrokeColor] = &OpSetStrokeColor;
        table[(int)ContentOperator.SetStrokeColorN] = &OpSetStrokeColor;
    }

    /// <summary>Registers the text operators.</summary>
    /// <param name="table">The table.</param>
    private static void RegisterText(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.BeginText] = &OpBeginText;
        table[(int)ContentOperator.EndText] = &OpEndText;
        table[(int)ContentOperator.SetCharacterSpacing] = &OpSetCharacterSpacing;
        table[(int)ContentOperator.SetWordSpacing] = &OpSetWordSpacing;
        table[(int)ContentOperator.SetHorizontalScaling] = &OpSetHorizontalScaling;
        table[(int)ContentOperator.SetLeading] = &OpSetLeading;
        table[(int)ContentOperator.SetFont] = &OpSetFont;
        table[(int)ContentOperator.SetRenderMode] = &OpSetRenderMode;
        table[(int)ContentOperator.SetRise] = &OpSetRise;
        table[(int)ContentOperator.MoveText] = &OpMoveText;
        table[(int)ContentOperator.MoveTextSetLeading] = &OpMoveTextSetLeading;
        table[(int)ContentOperator.SetTextMatrix] = &OpSetTextMatrix;
        table[(int)ContentOperator.NextLine] = &OpNextLine;
        table[(int)ContentOperator.ShowText] = &OpShowText;
        table[(int)ContentOperator.ShowTextArray] = &OpShowTextArray;
        table[(int)ContentOperator.NextLineShowText] = &OpNextLineShowText;
        table[(int)ContentOperator.SetSpacingNextLineShowText] = &OpSpacingNextLineShowText;
        table[(int)ContentOperator.SetGlyphWidthAndBounds] = &OpSetGlyphWidthAndBounds;
    }

    /// <summary>Registers XObjects, inline images, shadings and marked content.</summary>
    /// <param name="table">The table.</param>
    private static void RegisterOther(delegate*<ContentInterpreter, ref ContentReader, void>[] table)
    {
        table[(int)ContentOperator.PaintXObject] = &OpPaintXObject;
        table[(int)ContentOperator.BeginInlineImage] = &OpInlineImage;
        table[(int)ContentOperator.PaintShading] = &OpPaintShading;
        table[(int)ContentOperator.BeginMarkedContent] = &OpBeginMarkedContent;
        table[(int)ContentOperator.BeginMarkedContentProperties] = &OpBeginMarkedContentProperties;
        table[(int)ContentOperator.EndMarkedContent] = &OpEndMarkedContent;
    }

    /// <summary>Does nothing; the handler for operators that do not affect rendering.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpIgnore(ContentInterpreter self, ref ContentReader reader)
    {
        // Compatibility sections, flatness, rendering intent, marked points and stray inline image keywords change nothing drawn.
    }

    /// <summary>Runs one operator, treating a damaged operand as a skipped operator.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader holding its operands.</param>
    private void Dispatch(ContentOperator op, ref ContentReader reader)
    {
        var handler = Handlers[(int)op];
        try
        {
            handler(this, ref reader);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            // A damaged object affects only the operator that used it.
        }
    }
}
