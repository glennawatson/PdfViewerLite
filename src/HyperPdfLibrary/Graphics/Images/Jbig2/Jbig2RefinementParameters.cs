// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The parameters of the generic refinement region decoding procedure (T.88 table 6).</summary>
/// <param name="Template">The template, GRTEMPLATE: 0 or 1.</param>
/// <param name="TypicalPrediction">Whether typical prediction is on, TPGRON.</param>
/// <param name="ReferenceX">The reference bitmap's column offset, GRREFERENCEDX.</param>
/// <param name="ReferenceY">The reference bitmap's row offset, GRREFERENCEDY.</param>
/// <param name="At">The adaptive template pixels, GRAT: the first in the region, the second in the reference.</param>
internal readonly record struct Jbig2RefinementParameters(int Template, bool TypicalPrediction, int ReferenceX, int ReferenceY, Jbig2AtPixels At)
{
    /// <summary>The contexts of template 0.</summary>
    private const int Template0Contexts = 1 << 13;

    /// <summary>The contexts of template 1.</summary>
    private const int Template1Contexts = 1 << 10;

    /// <summary>Gets the number of contexts the template needs.</summary>
    internal int ContextCount => ContextCountFor(Template);

    /// <summary>Gets the number of contexts a refinement template needs.</summary>
    /// <param name="template">The template, 0 or 1.</param>
    /// <returns>The context count.</returns>
    internal static int ContextCountFor(int template) => template == 0 ? Template0Contexts : Template1Contexts;
}
