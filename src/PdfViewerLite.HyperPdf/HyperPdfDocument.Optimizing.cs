// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using PdfViewerLite.Core.Optimizing;

namespace PdfViewerLite.HyperPdf;

/// <content>Writes an optimised copy of the open document, unsaved edits included, with the managed optimiser.</content>
public sealed partial class HyperPdfDocument : IDocumentOptimizer
{
    /// <inheritdoc/>
    public async Task<OptimizeReport> OptimizeAsync(Stream destination, OptimizeSettings settings, IProgress<OptimizeProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(settings);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var snapshot = TakeSnapshot();
            var options = OptimizeMapping.ToOptions(settings);
            var report = await PdfOptimizer.OptimizeSnapshotAsync(snapshot, destination, options, OptimizeMapping.ToProgress(progress), cancellationToken).ConfigureAwait(false);
            return OptimizeMapping.ToReport(report);
        }
        catch (PdfException ex)
        {
            throw new InvalidDataException(ex.Message, ex);
        }
    }

    /// <summary>Copies the open document with its unsaved edits while no edit or save runs, so the optimiser works without holding the edit gate.</summary>
    /// <returns>The snapshot.</returns>
    /// <exception cref="ObjectDisposedException">The document was closed.</exception>
    private PdfDocument TakeSnapshot()
    {
        lock (_editGate)
        {
            return Annotations.OpenSnapshot() ?? throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
