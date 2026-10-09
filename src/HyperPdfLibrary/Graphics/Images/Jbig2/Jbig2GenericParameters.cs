// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The parameters of the arithmetic generic region decoding procedure (T.88 table 2).</summary>
/// <param name="Template">The template, GBTEMPLATE, 0 to 3.</param>
/// <param name="TypicalPrediction">Whether typical prediction is on, TPGDON.</param>
/// <param name="At">The adaptive template pixels, GBAT.</param>
internal readonly record struct Jbig2GenericParameters(int Template, bool TypicalPrediction, Jbig2AtPixels At)
{
    /// <summary>The template whose contexts have 16 bits.</summary>
    private const int LargestTemplate = 0;

    /// <summary>The template whose contexts have 13 bits.</summary>
    private const int MediumTemplate = 1;

    /// <summary>The contexts of template 0.</summary>
    private const int LargeContexts = 1 << 16;

    /// <summary>The contexts of template 1.</summary>
    private const int MediumContexts = 1 << 13;

    /// <summary>The contexts of templates 2 and 3.</summary>
    private const int SmallContexts = 1 << 10;

    /// <summary>Gets the number of contexts the template needs.</summary>
    internal int ContextCount => ContextCountFor(Template);

    /// <summary>Gets the number of contexts a generic template needs.</summary>
    /// <param name="template">The template, 0 to 3.</param>
    /// <returns>The context count.</returns>
    internal static int ContextCountFor(int template) => template switch
    {
        LargestTemplate => LargeContexts,
        MediumTemplate => MediumContexts,
        _ => SmallContexts,
    };
}
