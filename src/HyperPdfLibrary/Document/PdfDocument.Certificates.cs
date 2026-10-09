// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Document;

/// <content>Opening documents encrypted for certificates (the public-key security handler).</content>
public sealed partial class PdfDocument
{
    /// <summary>Opens a file encrypted for certificates.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="certificate">A recipient's certificate with its RSA private key.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the certificate is not a recipient.</exception>
    public static PdfDocument OpenWithCertificate(string path, X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return OpenWith(path, new() { Certificate = certificate });
    }

    /// <summary>Opens a document held in memory that is encrypted for certificates; the bytes are kept, not copied.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="certificate">A recipient's certificate with its RSA private key.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the certificate is not a recipient.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="certificate"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDocument OpenWithCertificate(byte[] bytes, X509Certificate2 certificate) =>
        OpenWith(bytes, new() { Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate)) });
}
