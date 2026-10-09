// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Creates resources and form streams for generated annotation appearances.</summary>
internal static class AnnotationAppearanceResources
{
    /// <summary>Builds the resources PDFium gives a generated appearance: one graphics state with the annotation's opacity and a blend mode.</summary>
    /// <param name="context">The annotation.</param>
    /// <param name="blend">The blend mode name.</param>
    /// <returns>The resources.</returns>
    internal static PdfDictionary CreateResources(AnnotationContext context, KnownName blend)
    {
        var store = context.Store;
        var opacityValue = context.Annotation.Get(KnownName.CA);
        var opacity = PdfValue.FromReal(opacityValue.IsNull ? 1 : opacityValue.AsSingle(1));
        var state = new PdfDictionary(store);
        state.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        state.Set(KnownName.CA, opacity);
        state.Set(context.Cache.LowerCa, opacity);
        state.Set(KnownName.BM, PdfValue.FromName(blend));
        var states = new PdfDictionary(store);
        states.Set(context.Cache.GraphicsStateName, PdfValue.FromDictionary(state));
        var resources = new PdfDictionary(store);
        resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(states));
        return resources;
    }

    /// <summary>Wraps content in an in-memory Form XObject whose box is the rectangle it is fitted to.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The annotation.</param>
    /// <param name="rect">The box, in page space.</param>
    /// <param name="resources">The resources.</param>
    /// <returns>The appearance.</returns>
    internal static GeneratedAppearance Finish(ref PdfContentBuilder builder, AnnotationContext context, PdfRectangle rect, PdfDictionary resources) =>
        new(CreateForm(builder.ToArray(), context, rect, resources), null, rect);

    /// <summary>Creates an uncompressed in-memory Form XObject.</summary>
    /// <param name="content">The content.</param>
    /// <param name="context">The annotation.</param>
    /// <param name="box">The /BBox.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream CreateForm(byte[] content, AnnotationContext context, PdfRectangle box, PdfDictionary? resources)
    {
        var dictionary = new PdfDictionary(context.Store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        dictionary.Set(KnownName.BBox, PdfValue.FromArray(box.ToArray(context.Store)));
        if (resources is not null)
        {
            dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        return new(dictionary, content);
    }
}
