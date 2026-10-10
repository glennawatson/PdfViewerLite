// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Copies immutable managed contours into recorder-owned native conversion state.</summary>
internal static class SkiaPathConversion
{
    /// <summary>Creates a temporary native contour whose fill rule follows the managed path.</summary>
    /// <param name="builder">The recording device's reusable builder.</param>
    /// <param name="path">The managed contour.</param>
    /// <returns>The owned temporary contour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKPath Create(SKPathBuilder builder, PdfPath path) => Create(builder, path, path.FillRule == PdfPathFillRule.EvenOdd);

    /// <summary>Creates a temporary native contour using the requested fill rule.</summary>
    /// <param name="builder">The recording device's reusable builder.</param>
    /// <param name="path">The managed contour.</param>
    /// <param name="evenOdd">Whether to apply the even-odd rule.</param>
    /// <returns>The owned temporary contour.</returns>
    internal static SKPath Create(SKPathBuilder builder, PdfPath path, bool evenOdd)
    {
        foreach (var command in path.Commands)
        {
            AppendCommand(builder, command);
        }

        var native = builder.Detach();
        native.FillType = evenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        return native;
    }

    /// <summary>Appends one managed contour command.</summary>
    /// <param name="builder">The reusable native builder.</param>
    /// <param name="command">The managed command.</param>
    private static void AppendCommand(SKPathBuilder builder, PdfPathCommand command)
    {
        switch (command.Kind)
        {
            case PdfPathCommandKind.MoveTo:
                {
                    builder.MoveTo(command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.LineTo:
                {
                    builder.LineTo(command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.QuadraticTo:
                {
                    builder.QuadTo(command.Control1.X, command.Control1.Y, command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.CubicTo:
                {
                    builder.CubicTo(command.Control1.X, command.Control1.Y, command.Control2.X, command.Control2.Y, command.Point.X, command.Point.Y);
                    break;
                }

            case PdfPathCommandKind.Close:
                {
                    builder.Close();
                    break;
                }
        }
    }
}
