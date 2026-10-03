// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Speech;

namespace PdfViewerLite.Speech.Audio;

/// <summary>
/// Plays speech through PulseAudio's simple API, which PipeWire also provides. Audio is written in short pieces so Stop
/// takes effect at once; one stream is kept open while the sample rate stays the same.
/// </summary>
[DebuggerDisplay("PulseAudio")]
public sealed unsafe class PulseAudioOutput : IAudioOutput
{
    /// <summary>PulseAudio's 32 bit little endian float format.</summary>
    private const int Float32LittleEndian = 5;

    /// <summary>The playback direction.</summary>
    private const int Playback = 1;

    /// <summary>The samples written at a time, a tenth of a second at 24 kHz.</summary>
    private const int Chunk = 2_400;

    /// <summary>The microseconds of latency left that count as finished: what remains is the sound card's own delay.</summary>
    private const ulong FinishedLatency = 30_000;

    /// <summary>The latency PulseAudio reports on error.</summary>
    private const ulong LatencyError = ulong.MaxValue;

    /// <summary>How often to check whether the audio has finished playing.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);

    /// <summary>Serialises playback.</summary>
    private readonly Lock _gate = new();

    /// <summary>The open stream.</summary>
    private PulseStreamHandle? _stream;

    /// <summary>The open stream's sample rate.</summary>
    private int _rate;

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            PulseLibraryResolver.Install();
            return PulseLibraryResolver.TryLoad();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    /// <inheritdoc/>
    public Task PlayAsync(SpeechAudio audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return Task.Run(() => Play(audio, cancellationToken), CancellationToken.None);
    }

    /// <summary>Waits until the buffered audio has played, flushing it if cancelled.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    private static void WaitUntilPlayed(PulseStreamHandle stream, CancellationToken cancellationToken)
    {
        var latency = NativeMethods.PaSimpleGetLatency(stream, out _);
        while (latency is > FinishedLatency and not LatencyError)
        {
            if (cancellationToken.WaitHandle.WaitOne(PollInterval))
            {
                _ = NativeMethods.PaSimpleFlush(stream, out _);
                return;
            }

            latency = NativeMethods.PaSimpleGetLatency(stream, out _);
        }
    }

    /// <summary>Writes the audio piece by piece, then waits for it to finish playing.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="cancellationToken">Stops at once, discarding what is buffered.</param>
    private void Play(SpeechAudio audio, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var stream = Open(audio.SampleRate);
            if (stream is null)
            {
                return;
            }

            var samples = audio.Samples;
            for (var start = 0; start < samples.Length; start += Chunk)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _ = NativeMethods.PaSimpleFlush(stream, out _);
                    return;
                }

                var count = Math.Min(Chunk, samples.Length - start);
                fixed (float* data = &samples[start])
                {
                    _ = NativeMethods.PaSimpleWrite(stream, data, (nuint)(count * sizeof(float)), out _);
                }
            }

            WaitUntilPlayed(stream, cancellationToken);
        }
    }

    /// <summary>Opens a stream at a sample rate, keeping one already open at that rate. Callers hold the gate.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <returns>The stream, or <see langword="null"/> when there is no sound server.</returns>
    private PulseStreamHandle? Open(int rate)
    {
        if (_stream is not null && _rate == rate)
        {
            return _stream;
        }

        _stream?.Dispose();
        _stream = null;
        if (!IsAvailable)
        {
            return null;
        }

        var spec = new PulseSampleSpec(Float32LittleEndian, (uint)rate, 1);
        fixed (byte* name = "PdfViewerLite\0"u8)
        {
            fixed (byte* description = "Read aloud\0"u8)
            {
                var stream = NativeMethods.PaSimpleNew(null, name, Playback, null, description, &spec, null, null, out _);
                if (stream.IsInvalid)
                {
                    stream.Dispose();
                    return null;
                }

                _stream = stream;
                _rate = rate;
                return stream;
            }
        }
    }
}
