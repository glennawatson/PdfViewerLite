// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Forms;

/// <content>
/// Async forms of the form runtime operations. The objects are already in memory, so these run the synchronous core
/// without blocking or switching threads; they check the token before the work starts.
/// </content>
public sealed partial class PdfForm
{
    /// <summary>Resets form fields to their default values.</summary>
    /// <param name="action">The action.</param>
    /// <param name="cancellationToken">A token that cancels the reset before it starts.</param>
    /// <returns>The number of fields reset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<int> ResetAsync(ResetFormAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Reset(action));
    }

    /// <summary>Shows or hides the fields a hide action names.</summary>
    /// <param name="action">The action.</param>
    /// <param name="cancellationToken">A token that cancels the change before it starts.</param>
    /// <returns>The number of annotations changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<int> SetHiddenAsync(HideAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SetHidden(action));
    }

    /// <summary>Collects the data a submit-form action would send. Nothing is sent.</summary>
    /// <param name="action">The action.</param>
    /// <param name="cancellationToken">A token that cancels the collection before it starts.</param>
    /// <returns>The submission.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<PdfFormSubmission> CreateSubmissionAsync(SubmitFormAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreateSubmission(action));
    }

    /// <summary>Reads the order calculated fields are recalculated in.</summary>
    /// <param name="cancellationToken">A token that cancels the read before it starts.</param>
    /// <returns>The field names in calculation order.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<string[]> GetCalculationOrderAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<string> names = [];
        GetCalculationOrder(names);
        return ValueTask.FromResult(names.ToArray());
    }

    /// <summary>Reads the order the Tab key visits a page's form widgets.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">A token that cancels the read before it starts.</param>
    /// <returns>The widget indexes in tab order.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<int[]> GetTabSequenceAsync(int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<int> order = [];
        GetTabSequence(pageIndex, order);
        return ValueTask.FromResult(order.ToArray());
    }
}
