// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Finds and fixes the faults of a page box (/MediaBox, /CropBox and the others) so reading, checking and saving agree.</summary>
internal static class PageBoxes
{
    /// <summary>The description reported for a swapped or unusable page box.</summary>
    internal const string BadBoxMessage = "A page box was swapped, empty or not four numbers and was fixed.";

    /// <summary>The numbers in a box.</summary>
    private const int Corners = 4;

    /// <summary>How a box in a page dictionary reads.</summary>
    internal enum BoxState
    {
        /// <summary>The page has no such box.</summary>
        Missing = 0,

        /// <summary>The box is four numbers, lower left then upper right, with an area.</summary>
        Valid = 1,

        /// <summary>The corners are the wrong way round; swapping them gives a usable box.</summary>
        Swapped = 2,

        /// <summary>The box is not four numbers, is empty, or is too large to use.</summary>
        Unusable = 3,
    }

    /// <summary>Inspects a box in a page dictionary.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="key">The box key.</param>
    /// <returns>How the box reads.</returns>
    internal static BoxState Inspect(PdfDictionary page, KnownName key)
    {
        if (!page.ContainsKey(key) || page.Get(key).IsNull)
        {
            return BoxState.Missing;
        }

        Span<float> corners = stackalloc float[Corners];
        if (page.GetArray(key) is not { } array || array.ReadNumbers(corners) < Corners)
        {
            return BoxState.Unusable;
        }

        var box = PdfRectangle.FromCorners(corners[0], corners[1], corners[2], corners[3]);
        if (!PdfPage.IsUsableBox(box))
        {
            return BoxState.Unusable;
        }

        return corners[0] > corners[2] || corners[1] > corners[3] ? BoxState.Swapped : BoxState.Valid;
    }

    /// <summary>Reports a fault in a page's own boxes.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="number">The page object's number, or 0.</param>
    /// <param name="context">The open context, or <see langword="null"/>.</param>
    internal static void Report(PdfDictionary page, int number, PdfOpenContext? context)
    {
        if (context is null)
        {
            return;
        }

        ReportBox(page, KnownName.MediaBox, number, context);
        ReportBox(page, KnownName.CropBox, number, context);
    }

    /// <summary>Returns a corrected copy of a page's box, or <see langword="null"/> when it needs none.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="key">The box key.</param>
    /// <returns>The corrected box array, or <see langword="null"/> when the box is valid or missing; an unusable box has no correction here.</returns>
    internal static PdfArray? Corrected(PdfDictionary page, KnownName key) =>
        Inspect(page, key) == BoxState.Swapped && PdfRectangle.TryFromArray(page.GetArray(key), out var box)
            ? box.ToArray(page.Owner)
            : null;

    /// <summary>Reports one box when it is swapped or unusable.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="key">The box key.</param>
    /// <param name="number">The page object's number.</param>
    /// <param name="context">The open context.</param>
    private static void ReportBox(PdfDictionary page, KnownName key, int number, PdfOpenContext context)
    {
        var state = Inspect(page, key);
        if (state is BoxState.Swapped or BoxState.Unusable)
        {
            PdfOpenContext.Report(context, PdfDiagnosticCode.BadPageBox, BadBoxMessage, number, -1);
        }
    }
}
