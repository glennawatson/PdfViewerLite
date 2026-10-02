// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.Platform.Linux.Kde;

/// <summary>Supplies the KDE colour scheme and raises <see cref="PaletteChanged"/> when <c>kdeglobals</c> changes.</summary>
[DebuggerDisplay("{FilePath}")]
public sealed class KdeThemeSource : IDesktopThemeSource, IDisposable
{
    /// <summary>How long to wait for writes to settle before re-reading.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(300);

    /// <summary>The file watcher.</summary>
    private readonly FileSystemWatcher? _watcher;

    /// <summary>The debounce timer.</summary>
    private readonly Timer _timer;

    /// <summary>Initializes a new instance of the <see cref="KdeThemeSource"/> class using the user's <c>kdeglobals</c>.</summary>
    public KdeThemeSource()
        : this(Path.Combine(XdgDirectories.ConfigHome, "kdeglobals"))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="KdeThemeSource"/> class.</summary>
    /// <param name="filePath">The <c>kdeglobals</c> path.</param>
    public KdeThemeSource(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        FilePath = filePath;
        _timer = new(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
        Palette = Read(filePath);
        var directory = Path.GetDirectoryName(filePath);
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        _watcher = new(directory, Path.GetFileName(filePath)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc/>
    public event EventHandler? PaletteChanged;

    /// <summary>Gets the watched file.</summary>
    public string FilePath { get; }

    /// <inheritdoc/>
    public DesktopPalette? Palette { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _watcher?.Dispose();
        _timer.Dispose();
    }

    /// <summary>Reads and parses the file.</summary>
    /// <param name="filePath">The file.</param>
    /// <returns>The palette or <see langword="null"/>.</returns>
    private static DesktopPalette? Read(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? KdeGlobalsParser.Parse(File.ReadAllText(filePath)) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Schedules a reload.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnFileEvent(object sender, FileSystemEventArgs e) => _ = _timer.Change(SettleTime, Timeout.InfiniteTimeSpan);

    /// <summary>Re-reads the file and raises <see cref="PaletteChanged"/> when the palette differs.</summary>
    private void Reload()
    {
        var palette = Read(FilePath);
        if (palette == Palette)
        {
            return;
        }

        Palette = palette;
        PaletteChanged?.Invoke(this, EventArgs.Empty);
    }
}
