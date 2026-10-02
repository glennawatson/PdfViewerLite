// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Documents;

/// <summary>
/// Raises <see cref="Changed"/> on a thread pool thread shortly after a file is modified or atomically replaced.
/// Bursts of change notifications are coalesced.
/// </summary>
[DebuggerDisplay("{FilePath}")]
public sealed class DocumentFileWatcher : IDisposable
{
    /// <summary>How long the file must be quiet before <see cref="Changed"/> is raised.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(400);

    /// <summary>The underlying watcher.</summary>
    private readonly FileSystemWatcher? _watcher;

    /// <summary>The debounce timer.</summary>
    private readonly Timer _timer;

    /// <summary>Initializes a new instance of the <see cref="DocumentFileWatcher"/> class.</summary>
    /// <param name="filePath">The file to watch.</param>
    public DocumentFileWatcher(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = Path.GetFullPath(filePath);
        _timer = new(OnSettled, null, Timeout.Infinite, Timeout.Infinite);
        var directory = Path.GetDirectoryName(FilePath);
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        _watcher = new(directory, Path.GetFileName(FilePath)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>Raised after the file changed and settled.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the watched file.</summary>
    public string FilePath { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _watcher?.Dispose();
        _timer.Dispose();
    }

    /// <summary>Restarts the debounce timer.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnFileEvent(object sender, FileSystemEventArgs e) => _ = _timer.Change(SettleTime, Timeout.InfiniteTimeSpan);

    /// <summary>Raises <see cref="Changed"/> once the file exists again.</summary>
    /// <param name="state">Unused.</param>
    private void OnSettled(object? state)
    {
        if (File.Exists(FilePath))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
