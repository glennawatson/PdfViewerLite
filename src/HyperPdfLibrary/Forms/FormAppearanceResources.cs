// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Wraps widget content in a Form XObject with its font resources.</summary>
internal static class FormAppearanceResources
{
    /// <summary>Wraps the drawn content in a Form XObject whose resources hold the font.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="font">The font, or <see langword="null"/> when the content uses none.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Finish(ref PdfContentBuilder builder, AppearanceContext context, in FormFrame frame, FormFont? font)
    {
        PdfDictionary? resources = null;
        if (font is not null)
        {
            PdfDictionary fonts = new(context.Store);
            fonts.Set(context.Store.Names.Intern(font.Name), font.Resource);
            resources = new(context.Store);
            resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        }

        return builder.ToFormXObject(context.Store, new(0, 0, frame.Width, frame.Height), frame.GetMatrix(), resources);
    }
}
