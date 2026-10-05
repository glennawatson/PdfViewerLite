// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Tools.Signing;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Signs and verifies detached release signatures.</summary>
internal static class SignDetachedCommand
{
    /// <summary>Signs archives using the certificate from a verified executable.</summary>
    /// <param name="args">The verified certificate source.</param>
    /// <returns>The command exit code.</returns>
    internal static int Run(string[] args)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(args.Length, 1);
        var (_, assets, hash) = SignAuthenticodeCommand.ReadInputs();
        DetachedSigner.Sign(assets, args[0], hash);
        Console.WriteLine("Signed and verified detached release signatures.");
        return 0;
    }
}
