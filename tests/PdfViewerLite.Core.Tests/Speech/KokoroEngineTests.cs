// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Speech.Kokoro;

namespace PdfViewerLite.Core.Tests.Speech;

/// <summary>Tests for <see cref="KokoroEngine"/> and <see cref="KokoroModel"/> that need no voice files.</summary>
public sealed class KokoroEngineTests
{
    /// <summary>Kokoro's output sample rate.</summary>
    private const int KokoroHertz = 24_000;

    /// <summary>The engine is not ready until its files are installed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsNotReadyWithoutFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"kokoro-{Guid.NewGuid():N}");
        using var engine = new KokoroEngine(directory);

        await Assert.That(engine.IsReady).IsFalse();
        await Assert.That(engine.Voices.Count).IsGreaterThan(0);
    }

    /// <summary>Every voice has a file to download and the model's files are all listed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsEveryFile()
    {
        foreach (var voice in KokoroModel.Voices)
        {
            var name = KokoroModel.VoiceFile(voice.Id);
            var listed = false;
            foreach (var file in KokoroModel.Files)
            {
                listed |= file.LocalName == name;
            }

            await Assert.That(listed).IsTrue();
        }

        await Assert.That(KokoroModel.SampleRate).IsEqualTo(KokoroHertz);
    }

    /// <summary>British voices are recognised from their id.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesTheAccent()
    {
        await Assert.That(KokoroModel.IsBritish("bf_emma")).IsTrue();
        await Assert.That(KokoroModel.IsBritish("af_heart")).IsFalse();
        await Assert.That(KokoroModel.LexiconFiles(true)[0]).IsEqualTo("gb_gold.json");
    }
}
