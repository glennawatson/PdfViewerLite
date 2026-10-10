// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Drawing;

/// <summary>Transforms managed drawing geometry.</summary>
public static class PdfDrawingGeometryExtensions
{
    /// <summary>Provides geometry operations for a drawing transform.</summary>
    /// <param name="matrix">The transform.</param>
    extension(Matrix3x2 matrix)
    {
        /// <summary>Gets the bounds of a transformed rectangle.</summary>
        /// <param name="rectangle">The rectangle.</param>
        /// <returns>The transformed bounds.</returns>
        public PdfRect MapRect(PdfRect rectangle)
        {
            var first = Vector2.Transform(new(rectangle.Left, rectangle.Top), matrix);
            var second = Vector2.Transform(new(rectangle.Right, rectangle.Top), matrix);
            var third = Vector2.Transform(new(rectangle.Right, rectangle.Bottom), matrix);
            var fourth = Vector2.Transform(new(rectangle.Left, rectangle.Bottom), matrix);
            var minimum = Vector2.Min(Vector2.Min(first, second), Vector2.Min(third, fourth));
            var maximum = Vector2.Max(Vector2.Max(first, second), Vector2.Max(third, fourth));
            return new(minimum.X, minimum.Y, maximum.X, maximum.Y);
        }
    }
}
