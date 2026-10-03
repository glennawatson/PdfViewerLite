// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Speech;

namespace PdfViewerLite.Platform.MacOS.Audio;

/// <summary>
/// Plays speech through a CoreAudio output queue on the default device. Each clip is queued in short buffers, the queue
/// is asked to stop once they have played, and the output waits until it has; Stop stops it at once.
/// </summary>
[DebuggerDisplay("CoreAudio")]
public sealed unsafe class AudioQueueOutput : IAudioOutput
{
    /// <summary>The samples in each queued buffer, a quarter of a second at 24 kHz.</summary>
    private const int BufferSamples = 6_000;

    /// <summary>The property telling whether a queue is running (kAudioQueueProperty_IsRunning, <c>'aqrn'</c>).</summary>
    private const uint IsRunningProperty = 0x6171726E;

    /// <summary>The offset of AudioQueueBuffer.mAudioData.</summary>
    private const int DataOffset = 8;

    /// <summary>The offset of AudioQueueBuffer.mAudioDataByteSize.</summary>
    private const int SizeOffset = 16;

    /// <summary>How often to check whether the queue has finished.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

    /// <summary>Serialises playback.</summary>
    private readonly Lock _gate = new();

    /// <summary>Gets a value indicating whether an output queue can be created.</summary>
    public bool IsAvailable
    {
        get
        {
            var queue = Create(BufferSamples);
            if (queue == 0)
            {
                return false;
            }

            _ = NativeMethods.DisposeQueue(queue, 1);
            return true;
        }
    }

    /// <inheritdoc/>
    public Task PlayAsync(SpeechAudio audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return Task.Run(() => Play(audio, cancellationToken), CancellationToken.None);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Each clip disposes its own queue.
    }

    /// <summary>Called by CoreAudio as each buffer finishes; the buffers are freed with the queue.</summary>
    /// <param name="userData">Unused.</param>
    /// <param name="queue">The queue.</param>
    /// <param name="buffer">The buffer.</param>
    [UnmanagedCallersOnly]
    private static void OnBufferDone(nint userData, nint queue, nint buffer)
    {
        // Nothing to do: every buffer of a clip is queued up front.
    }

    /// <summary>Creates an output queue for float samples at a rate.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <returns>The queue, or zero.</returns>
    private static nint Create(int rate)
    {
        var format = StreamDescription.MonoFloat(rate);
        nint queue = 0;
        return NativeMethods.NewOutputQueue(&format, &OnBufferDone, 0, 0, 0, 0, &queue) == 0 ? queue : 0;
    }

    /// <summary>Determines whether a queue is still playing.</summary>
    /// <param name="queue">The queue.</param>
    /// <returns><see langword="true"/> while running.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRunning(nint queue)
    {
        Span<uint> running = [0, sizeof(uint)];
        fixed (uint* value = running)
        {
            return NativeMethods.GetQueueProperty(queue, IsRunningProperty, value, value + 1) == 0 && running[0] != 0;
        }
    }

    /// <summary>Queues every sample in short buffers.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="samples">The samples.</param>
    /// <returns><see langword="true"/> when everything was queued.</returns>
    private static bool Enqueue(nint queue, ReadOnlySpan<float> samples)
    {
        for (var start = 0; start < samples.Length; start += BufferSamples)
        {
            var piece = samples.Slice(start, Math.Min(BufferSamples, samples.Length - start));
            var bytes = (uint)(piece.Length * sizeof(float));
            nint buffer = 0;
            if (NativeMethods.AllocateBuffer(queue, bytes, &buffer) != 0)
            {
                return false;
            }

            piece.CopyTo(new(*(void**)(buffer + DataOffset), piece.Length));
            *(uint*)(buffer + SizeOffset) = bytes;
            if (NativeMethods.EnqueueBuffer(queue, buffer, 0, 0) != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Plays the clip and waits for it to finish.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="cancellationToken">Stops at once.</param>
    private void Play(SpeechAudio audio, CancellationToken cancellationToken)
    {
        if (audio.Samples.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            var queue = Create(audio.SampleRate);
            if (queue == 0)
            {
                return;
            }

            try
            {
                if (!Enqueue(queue, audio.Samples) || NativeMethods.StartQueue(queue, 0) != 0)
                {
                    return;
                }

                _ = NativeMethods.FlushQueue(queue);
                _ = NativeMethods.StopQueue(queue, 0);
                while (IsRunning(queue) && !cancellationToken.WaitHandle.WaitOne(PollInterval))
                {
                    // Wait for the queued audio to be heard.
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    _ = NativeMethods.StopQueue(queue, 1);
                }
            }
            finally
            {
                _ = NativeMethods.DisposeQueue(queue, 1);
            }
        }
    }
}
