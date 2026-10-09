// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary;

/// <summary>Thrown when a document cannot be opened.</summary>
[DebuggerDisplay("PdfException: {Error} {Message}")]
public sealed class PdfException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="PdfException"/> class.</summary>
    public PdfException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfException"/> class.</summary>
    /// <param name="message">The message.</param>
    public PdfException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public PdfException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfException"/> class.</summary>
    /// <param name="error">Why the document could not be opened.</param>
    /// <param name="message">The message.</param>
    public PdfException(PdfError error, string message)
        : base(message) => Error = error;

    /// <summary>Gets why the document could not be opened.</summary>
    public PdfError Error { get; }
}
