// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <content>Fonts, graphics states, patterns, annotations and the form's default resources.</content>
internal sealed partial class ContentUsageScanner
{
    /// <summary>Gets the appearance keys of an annotation: normal, rollover and down.</summary>
    private static ReadOnlySpan<int> AppearanceKeys => [(int)KnownName.N, (int)KnownName.R, (int)KnownName.D];

    /// <summary>Handles <c>Tf</c>: records the font and, for a Type 3 font, what its glyphs use.</summary>
    /// <param name="name">The font's resource name.</param>
    /// <param name="resources">The resources in force.</param>
    /// <param name="isFixed">Whether the text is shown outside page content.</param>
    private void UseFont(PdfName name, PdfDictionary? resources, bool isFixed)
    {
        Use(resources, KnownName.Font, name);
        var raw = resources?.GetDictionary(KnownName.Font)?.GetRaw(name) ?? default;
        if (StoreReading.Resolve(_document.Objects, raw).AsDictionary() is not { } font)
        {
            return;
        }

        if (raw.IsReference)
        {
            var number = raw.AsReference().Number;
            FontNumbers[font] = number;
            _ = (isFixed ? ExcludedFonts : PageFonts).Add(number);
        }

        if (!font.GetName(KnownName.Subtype).Is(KnownName.Type3))
        {
            return;
        }

        // A Type 3 font without resources draws its glyphs with the page's.
        if (font.GetDictionary(KnownName.Resources) is { } glyphResources)
        {
            ScanFixedResources(glyphResources);
        }
        else
        {
            MarkUnsafe(resources);
        }
    }

    /// <summary>Handles <c>gs</c>: a soft mask's group and a graphics state's font are used where their size is unknown.</summary>
    /// <param name="name">The graphics state's resource name.</param>
    /// <param name="resources">The resources in force.</param>
    /// <param name="walk">The walk's settings.</param>
    private void UseGraphicsState(PdfName name, PdfDictionary? resources, WalkSettings walk)
    {
        Use(resources, KnownName.ExtGState, name);
        if (resources?.GetDictionary(KnownName.ExtGState)?.GetDictionary(name) is not { } state)
        {
            return;
        }

        if (state.Get(KnownName.SMask).AsDictionary() is { } mask)
        {
            WalkFixed(mask.GetRaw(KnownName.G), resources, walk.Depth);
        }

        if (state.GetArray(KnownName.Font) is { Count: > 0 } font && font.GetRaw(0).IsReference)
        {
            _ = ExcludedFonts.Add(font.GetRaw(0).AsReference().Number);
        }
    }

    /// <summary>Handles a pattern colour: a tiling pattern's content is used where its size is unknown.</summary>
    /// <param name="name">The pattern's resource name.</param>
    /// <param name="resources">The resources in force.</param>
    /// <param name="walk">The walk's settings.</param>
    private void UsePattern(PdfName name, PdfDictionary? resources, WalkSettings walk)
    {
        Use(resources, KnownName.Pattern, name);
        var raw = resources?.GetDictionary(KnownName.Pattern)?.GetRaw(name) ?? default;
        WalkFixed(raw, resources, walk.Depth);
    }

    /// <summary>Walks a form, pattern or appearance stream whose images keep their resolution and whose fonts stay whole.</summary>
    /// <param name="raw">The reference to the stream.</param>
    /// <param name="fallback">The resources a stream without its own uses.</param>
    /// <param name="depth">The nesting depth.</param>
    private void WalkFixed(PdfValue raw, PdfDictionary? fallback, int depth)
    {
        if (!raw.IsReference || depth >= PdfLimits.MaxDrawDepth || StoreReading.Resolve(_document.Objects, raw).AsStream() is not { } stream)
        {
            return;
        }

        if (!_fixedWalked.Add(raw.AsReference().Number))
        {
            return;
        }

        var own = stream.Dictionary.GetDictionary(KnownName.Resources);
        if (own is null)
        {
            MarkUnsafe(fallback);
        }

        WalkStream(stream, own ?? fallback, Matrix3x2.Identity, new(1, depth + 1, true));
    }

    /// <summary>Marks everything a resource dictionary names as used where its size is unknown.</summary>
    /// <param name="resources">The resource dictionary.</param>
    private void ScanFixedResources(PdfDictionary resources)
    {
        if (resources.GetDictionary(KnownName.XObject) is { } xobjects)
        {
            for (var i = 0; i < xobjects.Count; i++)
            {
                MarkFixedXObject(xobjects.GetValueAt(i), resources);
            }
        }

        if (resources.GetDictionary(KnownName.Font) is { } fonts)
        {
            for (var i = 0; i < fonts.Count; i++)
            {
                ExcludeFont(fonts.GetValueAt(i));
            }
        }
    }

    /// <summary>Marks an XObject used where its size is unknown: an image is fixed, a form is walked as fixed.</summary>
    /// <param name="raw">The reference to the XObject.</param>
    /// <param name="resources">The resources a form without its own uses.</param>
    private void MarkFixedXObject(PdfValue raw, PdfDictionary resources)
    {
        if (!raw.IsReference || StoreReading.Resolve(_document.Objects, raw).AsStream() is not { } xobject)
        {
            return;
        }

        if (xobject.Dictionary.GetName(KnownName.Subtype).Is(KnownName.Image))
        {
            RecordImage(raw.AsReference().Number, xobject.Dictionary, Matrix3x2.Identity, new(1, 0, true));
            return;
        }

        WalkFixed(raw, resources, 0);
    }

    /// <summary>Keeps a font whole.</summary>
    /// <param name="raw">The reference to the font.</param>
    private void ExcludeFont(PdfValue raw)
    {
        if (raw.IsReference)
        {
            _ = ExcludedFonts.Add(raw.AsReference().Number);
        }
    }

    /// <summary>Walks a page's annotation appearances as fixed.</summary>
    /// <param name="page">The page.</param>
    private void ScanAnnotations(PdfPage page)
    {
        if (page.Dictionary.GetArray(KnownName.Annots) is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i)?.GetDictionary(KnownName.AP) is { } appearances)
            {
                ScanAppearances(appearances, page.Resources);
            }
        }
    }

    /// <summary>Walks an annotation's appearance streams, each a stream or a dictionary of states.</summary>
    /// <param name="appearances">The /AP dictionary.</param>
    /// <param name="pageResources">The page's resources.</param>
    private void ScanAppearances(PdfDictionary appearances, PdfDictionary? pageResources)
    {
        foreach (var key in AppearanceKeys)
        {
            var raw = appearances.GetRaw((KnownName)key);
            var value = StoreReading.Resolve(_document.Objects, raw);
            if (value.Kind != PdfKind.Dictionary)
            {
                WalkFixed(raw, pageResources, 0);
                continue;
            }

            var states = value.AsDictionary()!;
            for (var i = 0; i < states.Count; i++)
            {
                WalkFixed(states.GetValueAt(i), pageResources, 0);
            }
        }
    }

    /// <summary>Keeps the interactive form's default fonts whole, as filled-in fields draw with any of their glyphs.</summary>
    private void ScanAcroForm()
    {
        if (_document.Catalog.GetDictionary(KnownName.AcroForm)?.GetDictionary(KnownName.DR)?.GetDictionary(KnownName.Font) is not { } fonts)
        {
            return;
        }

        for (var i = 0; i < fonts.Count; i++)
        {
            ExcludeFont(fonts.GetValueAt(i));
        }
    }
}
