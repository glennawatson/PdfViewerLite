// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Tools.VoiceModels;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the reference melo command.</summary>
internal static class ReferenceMeloCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 3)
        {
            await Console.Error.WriteLineAsync("Usage: PdfViewerLite.Tools voice reference-melo <voice folder> <upstream reference JSON> <output JSON>");
            return 1;
        }

        if (string.Equals(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase))
        {
            await Console.Error.WriteLineAsync("The native snapshot must not overwrite the upstream reference.");
            return 1;
        }

        var snapshot = MeloReferenceData.Create(args[0], args[1]);
        var output = Path.GetFullPath(args[2]);
        var partial = $"{output}.{Guid.NewGuid():N}.part";
        try
        {
            await File.WriteAllBytesAsync(partial, snapshot).ConfigureAwait(false);
            File.Move(partial, output, true);
        }
        finally
        {
            File.Delete(partial);
        }

        return 0;
    }
}
