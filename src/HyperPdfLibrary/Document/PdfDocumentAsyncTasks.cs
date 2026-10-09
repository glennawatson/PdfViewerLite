// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Coordinates asynchronous document work.</summary>
public static class PdfDocumentAsyncTasks
{
    /// <summary>Determines whether an exception from the synchronous core belongs in the returned task rather than being thrown at the call.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for cancellation and for a document that cannot be read.</returns>
    internal static bool IsTaskFault(Exception exception) => exception is OperationCanceledException or PdfException;
}
