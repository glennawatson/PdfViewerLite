// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>What every signature check in one document shares: the security store, its certificates and the options.</summary>
/// <param name="Store">The document security store.</param>
/// <param name="StoreCertificates">The store's certificates, loaded.</param>
/// <param name="Options">The validation options.</param>
[DebuggerDisplay("CmsContext: {StoreCertificates.Count} certificates")]
internal readonly record struct CmsContext(PdfSecurityStore Store, X509Certificate2Collection StoreCertificates, PdfSignatureValidationOptions Options)
{
    /// <summary>Creates the context, loading the store's certificates and skipping any that cannot be read.</summary>
    /// <param name="store">The document security store.</param>
    /// <param name="options">The validation options.</param>
    /// <returns>The context.</returns>
    internal static CmsContext Create(PdfSecurityStore store, PdfSignatureValidationOptions options)
    {
        var certificates = new X509Certificate2Collection();
        foreach (var der in store.Certificates)
        {
            try
            {
                _ = certificates.Add(X509CertificateLoader.LoadCertificate(der));
            }
            catch (CryptographicException)
            {
                // A damaged certificate in the store only means one fewer candidate for chain building.
            }
        }

        return new(store, certificates, options);
    }
}
