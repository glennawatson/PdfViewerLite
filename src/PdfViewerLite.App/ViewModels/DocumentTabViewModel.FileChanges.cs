// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Settings;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <content>Following changes other programs make to the open file.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>The file's last write time and length when it was loaded or last seen to change.</summary>
    private (DateTime Written, long Length) _fileStamp;

    /// <summary>Reloads a changed file, or offers to, depending on the user's choice. Unsaved edits are never discarded silently.</summary>
    internal void OnFileChanged()
    {
        var stamp = ReadFileStamp(FilePath);
        if (Environment.TickCount64 - Interlocked.Read(ref _savedAt) < SelfSaveWindowMilliseconds)
        {
            _fileStamp = stamp;
            return;
        }

        // macOS can report a write made just before watching began; a file whose time and length are unchanged is the one loaded.
        if (stamp == _fileStamp)
        {
            return;
        }

        _fileStamp = stamp;
        if (_services.Settings.FileChangeAction == FileChangeAction.AskToReload || Source.HasUnsavedChanges)
        {
            HasPendingReload = true;
            return;
        }

        Reload();
    }

    /// <summary>Reads what shows a file changed: its last write time and length.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The stamp, or the default when the file is missing.</returns>
    private static (DateTime Written, long Length) ReadFileStamp(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? (file.LastWriteTimeUtc, file.Length) : default;
    }

    /// <summary>Starts watching the file for changes, from the file as it is now.</summary>
    private void WatchFile()
    {
        _fileStamp = ReadFileStamp(FilePath);
        _fileWatch ??= FileChanges.Watch(FilePath)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => OnFileChanged(), static ex => Trace.TraceError(ex.ToString()));
    }
}
