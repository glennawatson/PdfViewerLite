// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Selects a generated annotation appearance by subtype.</summary>
internal static class AnnotationAppearanceDispatch
{
    /// <summary>The drawers by annotation subtype.</summary>
    private static readonly FrozenDictionary<KnownName, Func<AnnotationContext, GeneratedAppearance?>> Drawers =
        new Dictionary<KnownName, Func<AnnotationContext, GeneratedAppearance?>>
        {
            [KnownName.Square] = AnnotationShapeAppearance.DrawSquare,
            [KnownName.Circle] = AnnotationShapeAppearance.DrawCircle,
            [KnownName.Highlight] = AnnotationMarkupAppearance.DrawHighlight,
            [KnownName.Underline] = AnnotationMarkupAppearance.DrawUnderline,
            [KnownName.StrikeOut] = AnnotationMarkupAppearance.DrawStrikeOut,
            [KnownName.Squiggly] = AnnotationMarkupAppearance.DrawSquiggly,
            [KnownName.Ink] = AnnotationShapeAppearance.DrawInk,
            [KnownName.Text] = AnnotationTextAppearance.DrawTextIcon,
            [KnownName.FreeText] = AnnotationTextAppearance.DrawFreeText,
            [KnownName.Line] = AnnotationShapeAppearance.DrawLine,
            [KnownName.Polygon] = static context => AnnotationShapeAppearance.DrawPolygon(context, true),
            [KnownName.PolyLine] = static context => AnnotationShapeAppearance.DrawPolygon(context, false),
            [KnownName.Widget] = AnnotationTextAppearance.DrawWidget,
        }.ToFrozenDictionary();

    /// <summary>Draws an appearance for an annotation that has no normal appearance.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null when the subtype is not drawn or the annotation lacks what it needs.</returns>
    internal static GeneratedAppearance? Generate(AnnotationContext context)
    {
        var subtype = context.Annotation.GetName(KnownName.Subtype).ToKnownName();
        return Drawers.TryGetValue(subtype, out var drawer) ? drawer(context) : null;
    }
}
