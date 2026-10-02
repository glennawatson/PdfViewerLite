#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:project ../../src/PdfViewerLite.Speech/PdfViewerLite.Speech.csproj
#:include MeloReferenceData.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloFrontEnd.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloInput.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloLexicon.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloSymbols.cs
#:include ../../src/PdfViewerLite.Speech/Melo/SpellingToSound.cs
#:include ../../src/PdfViewerLite.Speech/Melo/WordPieceTokenizer.cs

using PdfViewerLite.Tools.VoiceModels;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: dotnet run --file reference_melo.cs -- <voice folder> <upstream reference JSON> <output JSON>");
    return 1;
}

if (string.Equals(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("The native snapshot must not overwrite the upstream reference.");
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
