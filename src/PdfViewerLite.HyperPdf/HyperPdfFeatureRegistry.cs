// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Forms.Scripting;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Core.Optimizing;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Core.Redaction;
using PdfViewerLite.Core.Signatures;
namespace PdfViewerLite.HyperPdf;

/// <summary>Resolves supported services without reflection or activation.</summary>
internal static class HyperPdfFeatureRegistry
{
    /// <summary>The feature types in the same order as their factories.</summary>
    private static readonly Type[] Types = [typeof(IAttachmentSource), typeof(ISignatureSource), typeof(IContentCheck), typeof(IPageManagementSource),
        typeof(IAnnotationEditor), typeof(ITextBoxEditor), typeof(IImageSignatureEditor), typeof(ITextLayerWriter), typeof(IPageExporter), typeof(IFormOrder),
            typeof(IFormActions), typeof(IFormHighlight), typeof(IFormFiller), typeof(IFormScriptSource), typeof(ILayerSource), typeof(IDocumentOptimizer),
                typeof(IDocumentRedactor), typeof(IRepairReport), typeof(ITaggedStructureSource), typeof(ITextLayoutSource)];

    /// <summary>Creates one lightweight service borrowing the document's resources.</summary>
    private static readonly Func<HyperPdfDocument, object>[] Factories = [static owner => new HyperPdfAttachmentSourceService(owner),
static owner => new HyperPdfSignatureSourceService(owner),
static owner => new HyperPdfContentCheckService(owner),
static owner => new HyperPdfPageManagementSourceService(owner),
static owner => new HyperPdfAnnotationEditorService(owner),
static owner => new HyperPdfTextBoxEditorService(owner),
static owner => new HyperPdfImageSignatureEditorService(owner),
static owner => new HyperPdfTextLayerWriterService(owner),
static owner => new HyperPdfPageExporterService(owner),
static owner => new HyperPdfFormOrderService(owner),
static owner => new HyperPdfFormActionsService(owner),
static owner => new HyperPdfFormHighlightService(owner),
static owner => new HyperPdfFormFillerService(owner),
static owner => new HyperPdfFormScriptSourceService(owner),
static owner => new HyperPdfLayerSourceService(owner),
static owner => new HyperPdfDocumentOptimizerService(owner),
static owner => new HyperPdfDocumentRedactorService(owner),
static owner => new HyperPdfRepairReportService(owner),
static owner => new HyperPdfTaggedStructureSourceService(owner),
static owner => new HyperPdfTextLayoutSourceService(owner)];

    /// <summary>Gets a stable service while leaving feature resources lazy.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The feature, or null when unsupported.</returns>
    internal static object? Get(HyperPdfDocument owner, Type featureType)
    {
        lock (owner.EditGate)
        {
            ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
            return GetOpen(owner, featureType);
        }
    }

    /// <summary>Resolves a feature with the owner gate held.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The feature, or null when unsupported.</returns>
    private static object? GetOpen(HyperPdfDocument owner, Type featureType)
    {
        if (featureType.IsInstanceOfType(owner))
        {
            return owner;
        }

        var index = Array.IndexOf(Types, featureType);
        if (index < 0)
        {
            return null;
        }

        var slots = owner.FeatureSlots ??= new object?[Types.Length];
        var feature = slots[index] ??= Factories[index](owner);
        return feature;
    }
}
