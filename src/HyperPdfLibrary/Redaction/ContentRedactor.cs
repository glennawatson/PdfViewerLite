// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Redaction;

/// <summary>Removes what lies under redacted areas from the objects of one content: text glyphs, images, line art and, inside forms, the same.</summary>
/// <param name="regions">The areas in user space.</param>
/// <param name="options">The options.</param>
/// <param name="tally">Receives the counts.</param>
/// <param name="cancellationToken">Checked between objects.</param>
[DebuggerDisplay("ContentRedactor: {regions.Length} areas")]
internal sealed class ContentRedactor(PdfRectangle[] regions, PdfRedactionOptions options, RedactionTally tally, CancellationToken cancellationToken)
{
    /// <summary>A hairline path or box has no area; it is widened by this much so a touch can find it.</summary>
    private const float HairlineSlack = 0.25F;

    /// <summary>The render mode of invisible text.</summary>
    private const int InvisibleMode = 3;

    /// <summary>Redacts every object of a content, and the content of the forms it paints.</summary>
    /// <param name="content">The content; changes are made to its objects.</param>
    internal void Redact(PdfPageContent content)
    {
        foreach (var item in content.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (item)
            {
                case PdfTextObject text:
                    {
                        RedactText(text);
                        break;
                    }

                case PdfImageObject image:
                    {
                        RedactImage(image, content);
                        break;
                    }

                case PdfPathObject or PdfShadingObject:
                    {
                        RedactLineArt(item);
                        break;
                    }

                case PdfFormObject form:
                    {
                        RedactForm(form);
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
        }
    }

    /// <summary>Widens a rectangle with no area a little, so lines and thin boxes can be touched.</summary>
    /// <param name="bounds">The bounds.</param>
    /// <returns>The bounds, widened where they are flat.</returns>
    private static PdfRectangle Widen(PdfRectangle bounds)
    {
        var left = bounds.Width > 0 ? bounds.Left : bounds.Left - HairlineSlack;
        var right = bounds.Width > 0 ? bounds.Right : bounds.Right + HairlineSlack;
        var bottom = bounds.Height > 0 ? bounds.Bottom : bounds.Bottom - HairlineSlack;
        var top = bounds.Height > 0 ? bounds.Top : bounds.Top + HairlineSlack;
        return new(left, bottom, right, top);
    }

    /// <summary>
    /// Marks the sequences a redacted text object sits in so the /ActualText and /Alt they carry go too: the replacement text
    /// would otherwise keep the removed words in the file.
    /// </summary>
    /// <param name="text">The text object.</param>
    private static void ScrubMarks(PdfTextObject text)
    {
        foreach (var mark in text.Marks)
        {
            if (mark.Properties is { } properties && (properties.ContainsKey(KnownName.ActualText) || properties.ContainsKey(KnownName.Alt)))
            {
                mark.Scrub();
            }
        }
    }

    /// <summary>Removes the glyphs under the areas.</summary>
    /// <param name="text">The text object.</param>
    private void RedactText(PdfTextObject text)
    {
        if (text.IsOpaque || (text.RenderMode == InvisibleMode && !options.RemoveInvisibleText))
        {
            return;
        }

        var before = text.RemovedCount;
        foreach (var region in regions)
        {
            if (PageGeometry.Overlaps(Widen(text.OriginalBounds), region))
            {
                _ = text.RemoveGlyphsIn(region);
            }
        }

        if (text.RemovedCount == before)
        {
            return;
        }

        tally.GlyphsRemoved += text.RemovedCount - before;
        RememberCodes(text);
        ScrubMarks(text);
    }

    /// <summary>Notes the codes removed from a text object's font, so its /ToUnicode map can lose them.</summary>
    /// <param name="text">The text object.</param>
    private void RememberCodes(PdfTextObject text)
    {
        if (text.Font is not { } font)
        {
            return;
        }

        for (var i = 0; i < text.GlyphCount; i++)
        {
            if (text.IsGlyphRemoved(i))
            {
                tally.AddRemovedCode(font.Dictionary, text.Glyphs[i].Code);
            }
        }
    }

    /// <summary>Applies the image mode to an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="content">The content that paints it.</param>
    private void RedactImage(PdfImageObject image, PdfPageContent content)
    {
        if (options.Images == PdfRedactionImageMode.None || !Touches(Widen(image.OriginalBounds)))
        {
            return;
        }

        switch (options.Images)
        {
            case PdfRedactionImageMode.Remove:
                {
                    Remove(image);
                    break;
                }

            case PdfRedactionImageMode.RemoveUnlessInvisible:
                {
                    if (VisibleTouches(image))
                    {
                        Remove(image);
                    }

                    break;
                }

            default:
                {
                    Blank(image, content);
                    break;
                }
        }
    }

    /// <summary>Blanks the pixels of an image under the areas.</summary>
    /// <param name="image">The image.</param>
    /// <param name="content">The content that paints it.</param>
    private void Blank(PdfImageObject image, PdfPageContent content)
    {
        switch (ImageRedactor.Blank(image, regions, content.Resources, cancellationToken))
        {
            case BlankResult.Removed:
                {
                    Remove(image);
                    break;
                }

            case BlankResult.Blanked:
                {
                    tally.ImagesBlanked++;
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Deletes an image and counts it.</summary>
    /// <param name="image">The image.</param>
    private void Remove(PdfImageObject image)
    {
        image.Delete();
        tally.ImagesRemoved++;
    }

    /// <summary>Applies the line art mode to a path or shading.</summary>
    /// <param name="item">The object.</param>
    private void RedactLineArt(PdfPageObject item)
    {
        var remove = options.LineArt switch
        {
            PdfRedactionLineArtMode.RemoveCovered => PageGeometry.IsFinite(item.OriginalBounds) && Covers(item.OriginalBounds),
            PdfRedactionLineArtMode.RemoveTouched => VisibleTouches(item),
            _ => false,
        };
        if (!remove)
        {
            return;
        }

        item.Delete();
        tally.PathsRemoved++;
    }

    /// <summary>Redacts the content of a form the areas touch.</summary>
    /// <param name="form">The form object.</param>
    private void RedactForm(PdfFormObject form)
    {
        if (form.BoundingBox is not null && !Touches(Widen(form.OriginalBounds)))
        {
            return;
        }

        var inner = form.GetContent();
        Redact(inner);
        if (PdfPageContentEditing.IsModified(inner))
        {
            tally.FormsRewritten++;
        }
    }

    /// <summary>Determines whether an area touches the part of an object that shows, inside its clip.</summary>
    /// <param name="item">The object.</param>
    /// <returns><see langword="true"/> when an area overlaps the visible part.</returns>
    private bool VisibleTouches(PdfPageObject item)
    {
        var visible = item.OriginalBounds.Intersect(item.ClipBounds);
        return visible.Width >= 0 && visible.Height >= 0 && Touches(Widen(visible));
    }

    /// <summary>Determines whether any area overlaps a rectangle.</summary>
    /// <param name="bounds">The rectangle.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private bool Touches(PdfRectangle bounds)
    {
        foreach (var region in regions)
        {
            if (PageGeometry.Overlaps(bounds, region))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether one area holds a rectangle whole.</summary>
    /// <param name="bounds">The rectangle.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private bool Covers(PdfRectangle bounds)
    {
        foreach (var region in regions)
        {
            if (PageGeometry.Contains(region, bounds))
            {
                return true;
            }
        }

        return false;
    }
}
