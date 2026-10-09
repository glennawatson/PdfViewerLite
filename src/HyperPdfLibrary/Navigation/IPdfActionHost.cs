// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Navigation;

/// <summary>
/// The part of running an action that belongs to the application: moving around, opening things and anything that
/// leaves the document. The runner never sends form data or opens a file itself. It asks the host, and a host that
/// would send or import data must get the user's consent first.
/// </summary>
public interface IPdfActionHost
{
    /// <summary>Runs a named viewer action such as <c>NextPage</c> or <c>Print</c>.</summary>
    /// <param name="name">The action's name.</param>
    /// <param name="sourcePage">The page the action came from, or -1 when it is not from a page.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns><see langword="true"/> when the host ran it.</returns>
    ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken);

    /// <summary>Follows a go-to, URI, remote go-to, launch or embedded go-to action.</summary>
    /// <param name="action">The action.</param>
    /// <param name="sourcePage">The page the action came from, or -1 when it is not from a page.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns><see langword="true"/> when the host followed it.</returns>
    ValueTask<bool> NavigateAsync(PdfAction action, int sourcePage, CancellationToken cancellationToken);

    /// <summary>Offers form data to send. The host sends it only if the user agrees.</summary>
    /// <param name="submission">The data the action would send.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns><see langword="true"/> when the host sent it.</returns>
    ValueTask<bool> SubmitFormAsync(PdfFormSubmission submission, CancellationToken cancellationToken);

    /// <summary>Offers to import form data from a file. The host imports it only if the user agrees.</summary>
    /// <param name="action">The import action.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns><see langword="true"/> when the host imported it.</returns>
    ValueTask<bool> ImportDataAsync(ImportDataAction action, CancellationToken cancellationToken);
}
