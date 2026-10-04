#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:project ../../src/PdfViewerLite.Speech/PdfViewerLite.Speech.csproj
#:include MeloFiles.cs
#:include MeloReferenceData.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloFrontEnd.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloInput.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloLexicon.cs
#:include ../../src/PdfViewerLite.Speech/Melo/MeloSymbols.cs
#:include ../../src/PdfViewerLite.Speech/Melo/SpellingToSound.cs
#:include ../../src/PdfViewerLite.Speech/Melo/WordPieceTokenizer.cs

using PdfViewerLite.Speech.Melo;
using PdfViewerLite.Tools.VoiceModels;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: dotnet run --file check_melo.cs -- <source manifest> <voice folder> <upstream reference JSON>");
    return 1;
}

await MeloFiles.ValidateAsync(MeloFiles.Load(args[0]), args[1]).ConfigureAwait(false);

_ = MeloReferenceData.Create(args[1], args[2]);

const string text = "The garden behind the library had been neglected for years, and the fountain had long since stopped.";

const int sampleRate = 44_100;

const double minLevel = 0.001;

const float maxPeak = 2;

using var engine = new MeloEngine(args[1]);

foreach (var voice in MeloModel.Voices)
{
    var audio = await engine.SynthesizeAsync(text, voice.Id, 1, CancellationToken.None).ConfigureAwait(false);
    double sum = 0;
    float peak = 0;
    foreach (var sample in audio.Samples)
    {
        if (!float.IsFinite(sample))
        {
            throw new InvalidDataException($"{voice.Id} produced a non-finite audio sample.");
        }

        sum += sample * (double)sample;
        peak = Math.Max(peak, Math.Abs(sample));
    }

    var level = Math.Sqrt(sum / Math.Max(1, audio.Samples.Length));
    if (audio.SampleRate != sampleRate || audio.Samples.Length == 0 || level < minLevel || peak >= maxPeak)
    {
        throw new InvalidDataException($"{voice.Id} produced invalid or silent audio.");
    }

    Console.WriteLine($"{voice.Id}: {audio.Samples.Length} finite samples at {audio.SampleRate} Hz, RMS {level:F4}, peak {peak:F4}");
}

return 0;
