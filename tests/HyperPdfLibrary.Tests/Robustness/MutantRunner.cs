// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Opens a damaged file on a worker thread, reads everything from it and reports a crash or hang.</summary>
internal static class MutantRunner
{
    /// <summary>How long one file may run before its token is cancelled.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>How long one file may run before it counts as hung even though it ignores the token.</summary>
    private static readonly TimeSpan HardLimit = TimeSpan.FromSeconds(20);

    /// <summary>Opens and reads one file. Only <see cref="PdfException"/> is an acceptable failure.</summary>
    /// <param name="file">The damaged file.</param>
    /// <returns>Whether the file opened, and a description of the problem or <see langword="null"/> when it behaved.</returns>
    internal static async Task<MutantResult> RunAsync(byte[] file)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        var token = timeout.Token;
        try
        {
            return await Task.Run(() => Exercise(file, token), CancellationToken.None).WaitAsync(HardLimit);
        }
        catch (TimeoutException)
        {
            return new(false, "hung past the hard limit");
        }
    }

    /// <summary>Opens a file and reads everything from it.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="token">Cancelled when the file has run too long.</param>
    /// <returns>Whether the file opened, and a description of the problem or <see langword="null"/>.</returns>
    private static MutantResult Exercise(byte[] bytes, CancellationToken token)
    {
        try
        {
            using var document = PdfDocument.OpenWith(bytes, new PdfOpenOptions { CancellationToken = token });
            _ = DocumentExerciser.Read(document);
            return new(true, null);
        }
        catch (PdfException)
        {
            return new(false, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }
}
