// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Objects;

/// <summary>What opening a store needs besides the file: the credentials and the open context.</summary>
/// <param name="Password">The password for the standard security handler, or <see langword="null"/>.</param>
/// <param name="Certificate">A recipient's certificate with its private key for the public-key handler, or <see langword="null"/>.</param>
/// <param name="Context">The cancellation and diagnostics context, or <see langword="null"/>.</param>
internal readonly record struct OpenCredentials(string? Password, X509Certificate2? Certificate, PdfOpenContext? Context);
