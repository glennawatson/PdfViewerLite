// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Redaction;

namespace PdfViewerLite.HyperPdf;

/// <content>Applies redaction marks to a private copy and writes it, so the open document and its file are never changed.</content>
public sealed partial class HyperPdfDocument : IDocumentRedactor
{
    /// <inheritdoc/>
    public async Task<RedactionReport> ApplyAsync(Stream destination, RedactionSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(settings);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var options = ToOptions(settings);
        try
        {
            // The copy carries the marks and every unsaved edit; the open document keeps its marks and stays as it is.
            var copy = HyperPdfLibrary.Document.PdfDocumentOptimizing.OpenWorkingCopy(_document);
            var report = await Task.Run(() => PdfRedactor.Apply(copy, options, cancellationToken), cancellationToken).ConfigureAwait(false);
            await PdfCompactWriter.SaveAsync(copy.Objects, options.Layout, destination, cancellationToken).ConfigureAwait(false);
            return new(report.Regions, report.Pages, report.GlyphsRemoved, report.ImagesRemoved + report.ImagesBlanked, report.PathsRemoved, report.AnnotationsRemoved);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Turns the app's choices into the library's options.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The options.</returns>
    private static PdfRedactionOptions ToOptions(RedactionSettings settings) => PdfRedactionOptions.Default with
    {
        Images = settings.Images switch
        {
            RedactionImageChoice.Keep => PdfRedactionImageMode.None,
            RedactionImageChoice.Remove => PdfRedactionImageMode.Remove,
            _ => PdfRedactionImageMode.BlankPixels,
        },
        LineArt = settings.LineArt switch
        {
            RedactionLineArtChoice.Keep => PdfRedactionLineArtMode.None,
            RedactionLineArtChoice.RemoveTouched => PdfRedactionLineArtMode.RemoveTouched,
            _ => PdfRedactionLineArtMode.RemoveCovered,
        },
        RemoveInvisibleText = settings.RemoveHiddenText,
        Annotations = settings.RemoveLinksAndComments ? PdfRedactionAnnotationMode.RemoveTouched : PdfRedactionAnnotationMode.Keep,
        ScrubMetadata = settings.ScrubMetadata,
    };
}
