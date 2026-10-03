// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Speech;
using PdfViewerLite.Http.Speech;
using PdfViewerLite.Speech.Audio;
using PdfViewerLite.Speech.Kokoro;

namespace PdfViewerLite.App.Services;

/// <summary>How the app reads aloud: where the on-device voice lives, how engines and sound output are made, and how the voice is downloaded.</summary>
/// <param name="VoiceDirectory">The folder holding the on-device voice.</param>
/// <param name="VoiceFiles">The files the on-device voice needs.</param>
/// <param name="CreateEngine">Creates the engine the settings choose, given the voice folder.</param>
/// <param name="CreateAudio">Creates the sound output.</param>
/// <param name="DownloadVoice">Downloads the missing voice files into the voice folder, reporting the fraction done.</param>
[DebuggerDisplay("{VoiceDirectory}")]
public sealed record SpeechSetup(
    string VoiceDirectory,
    IReadOnlyList<SpeechModelFile> VoiceFiles,
    Func<AppSettings, string, ISpeechEngine> CreateEngine,
    Func<IAudioOutput> CreateAudio,
    Func<IProgress<double>, CancellationToken, Task> DownloadVoice)
{
    /// <summary>Gets the default voice folder, <c>~/.local/share/pdfviewerlite/voices</c> on Linux.</summary>
    public static string DefaultVoiceDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "pdfviewerlite", "voices");

    /// <summary>Gets the voice files still to download.</summary>
    public long MissingBytes => SpeechModelDownloader.ApproximateBytes(SpeechModelDownloader.Missing(VoiceFiles, VoiceDirectory));

    /// <summary>Creates the default setup: Kokoro on this computer, or Azure with the person's key, played through PulseAudio or PipeWire.</summary>
    /// <returns>The setup.</returns>
    public static SpeechSetup CreateDefault() =>
        new(
            DefaultVoiceDirectory,
            KokoroModel.Files,
            CreateEngineFor,
            static () => new PulseAudioOutput(),
            static (progress, cancellationToken) => SpeechModelDownloader.DownloadAsync(KokoroModel.Files, DefaultVoiceDirectory, progress, cancellationToken));

    /// <summary>Creates the engine the settings choose.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="voiceDirectory">The on-device voice folder.</param>
    /// <returns>The engine.</returns>
    public static ISpeechEngine CreateEngineFor(AppSettings settings, string voiceDirectory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.SpeechEngine == SpeechEngineChoice.Azure
            ? new AzureSpeechEngine(new(settings.AzureSpeechKey, settings.AzureSpeechRegion))
            : new KokoroEngine(voiceDirectory);
    }
}
