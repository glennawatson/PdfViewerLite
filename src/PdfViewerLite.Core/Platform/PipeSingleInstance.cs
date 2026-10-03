// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.Core.Platform;

/// <summary>
/// Keeps one running window per user on Windows and macOS. The first launch owns a named mutex and listens on a named
/// pipe (a Unix domain socket on macOS); later launches send their documents down the pipe and exit. The pipe accepts
/// only the same user. Each message is the activation token on the first line, then one path or URI per line.
/// </summary>
[DebuggerDisplay("{_name}")]
public sealed class PipeSingleInstance : ISingleInstance
{
    /// <summary>How long a forwarding launch waits for the running instance.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The pipe and mutex name.</summary>
    private readonly string _name;

    /// <summary>The ownership claim.</summary>
    private readonly Mutex _claim;

    /// <summary>Stops listening.</summary>
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Requests received from other launches.</summary>
    private readonly Signal<OpenRequest> _openRequests = new();

    /// <summary>Initializes a new instance of the <see cref="PipeSingleInstance"/> class.</summary>
    /// <param name="name">The pipe and mutex name.</param>
    /// <param name="claim">The ownership claim, already held.</param>
    private PipeSingleInstance(string name, Mutex claim)
    {
        _name = name;
        _claim = claim;

        // The first pipe exists before the claim is returned, so a launch straight after finds it.
        var first = CreateServer(name);
        _ = Task.Run(() => ListenAsync(first));
    }

    /// <inheritdoc/>
    public IObservable<OpenRequest> OpenRequests => _openRequests;

    /// <summary>Gets the name used for the current user.</summary>
    /// <param name="application">The application name.</param>
    /// <returns>A name safe for pipes and mutexes, for example <c>PdfViewerLite-glenn</c>.</returns>
    public static string NameFor(string application)
    {
        ArgumentException.ThrowIfNullOrEmpty(application);
        var user = Environment.UserName;
        var builder = new StringBuilder(application.Length + 1 + user.Length).Append(application).Append('-');
        foreach (var c in user)
        {
            _ = builder.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }

        return builder.ToString();
    }

    /// <summary>Encodes a request as the message sent down the pipe.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The message.</returns>
    public static string Encode(OpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder().Append(request.ActivationToken).Append('\n');
        foreach (var uri in request.Uris)
        {
            if (uri.AsSpan().IndexOfAny('\r', '\n') < 0)
            {
                _ = builder.Append(uri).Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes a message received from the pipe.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The request.</returns>
    public static OpenRequest Decode(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var lines = message.Split('\n');
        var uris = new List<string>(Math.Max(0, lines.Length - 1));
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                uris.Add(lines[i]);
            }
        }

        return new(uris, lines[0].Length > 0 ? lines[0] : null);
    }

    /// <summary>Sends a request to the running instance.</summary>
    /// <param name="name">The pipe name, from <see cref="NameFor"/>.</param>
    /// <param name="request">The documents and activation token.</param>
    /// <returns><see langword="true"/> when a running instance took the request.</returns>
    public static async Task<bool> TryForwardAsync(string name, OpenRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            await using var client = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var timeout = new CancellationTokenSource(ConnectTimeout);
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
            var bytes = Encoding.UTF8.GetBytes(Encode(request));
            await client.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            await client.FlushAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Claims the single running window for this user.</summary>
    /// <param name="name">The pipe and mutex name, from <see cref="NameFor"/>.</param>
    /// <returns>The claim, or <see langword="null"/> when another process holds it.</returns>
    public static PipeSingleInstance? TryStart(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Mutex claim;
        try
        {
            claim = new(true, name, out var createdNew);
            if (!createdNew)
            {
                claim.Dispose();
                return null;
            }
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return new(name, claim);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
        _claim.Dispose();
        _openRequests.Dispose();
    }

    /// <summary>Creates a pipe ready for the next launch.</summary>
    /// <param name="name">The pipe name.</param>
    /// <returns>The pipe.</returns>
    private static NamedPipeServerStream CreateServer(string name) =>
        new(name, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

    /// <summary>Accepts one connection after another until disposed.</summary>
    /// <param name="first">The first pipe, already created.</param>
    /// <returns>A task.</returns>
    private async Task ListenAsync(NamedPipeServerStream first)
    {
        var token = _stop.Token;
        var server = first;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                    _openRequests.OnNext(Decode(message));
                }

                server = CreateServer(_name);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Single instance pipe failed: {ex.Message}");
                server = CreateServer(_name);
            }
        }

        await server.DisposeAsync().ConfigureAwait(false);
    }
}
