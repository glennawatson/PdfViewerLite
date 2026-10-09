// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Forms;

namespace PdfViewerLite.HyperPdf;

/// <content>The form runtime: calculation order, tab order, the tint over fillable fields, and button and page actions.</content>
public sealed partial class HyperPdfDocument : IFormOrder, IFormActions, IFormHighlight
{
    /// <summary>The bits to shift the alpha by when a tint is packed into one number.</summary>
    private const int AlphaShift = 32;

    /// <summary>The tint over fillable fields, packed by <see cref="Pack"/> so one atomic write publishes it.</summary>
    private long _highlight = Pack(FormHighlight.Default);

    /// <inheritdoc/>
    public FormHighlight Highlight
    {
        get => Unpack(Volatile.Read(ref _highlight));
        set
        {
            if (IsDisposed)
            {
                return;
            }

            Volatile.Write(ref _highlight, Pack(value));

            // The renderer holds the tint it was made with, so the next page drawn uses a new one.
            ResetRenderer();
        }
    }

    /// <inheritdoc/>
    public void GetCalculationOrder(List<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!IsDisposed)
        {
            _document.Form.GetCalculationOrder(output);
        }
    }

    /// <inheritdoc/>
    public void GetTabOrder(int pageIndex, List<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!IsDisposed)
        {
            _document.Form.GetTabSequence(pageIndex, output);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<FormActionResult> RunWidgetActionAsync(int pageIndex, int index, IFormActionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (IsDisposed || (uint)pageIndex >= (uint)PageCount
            || _document.GetPage(pageIndex).Dictionary.GetArray(KnownName.Annots) is not { } annotations
            || (uint)index >= (uint)annotations.Count
            || annotations.GetDictionary(index)?.GetDictionary(KnownName.A) is not { } action)
        {
            return FormActionResult.None;
        }

        var node = _document.ReadActionNode(action);
        return Complete(await CreateRunner(host).RunAsync(node, pageIndex, cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc/>
    public async ValueTask<FormActionResult> RunPageOpenedAsync(int pageIndex, IFormActionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return IsDisposed
            ? FormActionResult.None
            : Complete(await CreateRunner(host).RunPageOpenedAsync(pageIndex, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Unpacks a tint packed by <see cref="Pack"/>.</summary>
    /// <param name="packed">The packed tint.</param>
    /// <returns>The tint.</returns>
    private static FormHighlight Unpack(long packed) => new((uint)packed, (byte)(packed >> AlphaShift));

    /// <summary>Packs a tint into one number.</summary>
    /// <param name="highlight">The tint.</param>
    /// <returns>The packed tint.</returns>
    private static long Pack(FormHighlight highlight) => ((long)highlight.Alpha << AlphaShift) | highlight.Color;

    /// <summary>Gets the renderer options, which carry the tint.</summary>
    /// <returns>The options.</returns>
    private PdfRenderOptions CreateRenderOptions()
    {
        var tint = Highlight;
        return PdfRenderOptions.Default with { FormHighlight = new(tint.Color, tint.Alpha) };
    }

    /// <summary>Creates a runner that hands what the library cannot do to the application's host.</summary>
    /// <param name="host">The application's host.</param>
    /// <returns>The runner.</returns>
    private PdfActionRunner CreateRunner(IFormActionHost host) => new(_document, new FormActionHostAdapter(_document, host));

    /// <summary>Records what actions changed, so pages draw again and the document is marked unsaved.</summary>
    /// <param name="result">What the runner reported.</param>
    /// <returns>The same result, for the viewer.</returns>
    private FormActionResult Complete(PdfActionResult result)
    {
        if ((result & PdfActionResult.Changed) != 0)
        {
            lock (_editGate)
            {
                if (!IsDisposed)
                {
                    Edited();
                }
            }
        }

        return (FormActionResult)(int)result;
    }
}
