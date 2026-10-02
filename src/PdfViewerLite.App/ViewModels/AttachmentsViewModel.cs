// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using PdfViewerLite.Core.Attachments;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// A tab's embedded files. The attachments panel only appears for documents that have some; a file is saved where the
/// user picks, and never opened automatically, so nothing runs that the user did not choose to run.
/// </summary>
[DebuggerDisplay("{Items.Count} attachments")]
public sealed class AttachmentsViewModel : ReactiveObject
{
    /// <summary>Bytes in a kilobyte.</summary>
    private const double Kilobyte = 1024;

    /// <summary>Bytes in a megabyte.</summary>
    private const double Megabyte = Kilobyte * Kilobyte;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="AttachmentsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public AttachmentsViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, this.WhenAnyValue(static vm => vm.Selected).Select(static item => item is not null));
    }

    /// <summary>Gets the embedded files.</summary>
    public ObservableCollection<DocumentAttachment> Items { get; } = [];

    /// <summary>Gets a value indicating whether the document has embedded files.</summary>
    public bool HasAttachments
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the selected file.</summary>
    public DocumentAttachment? Selected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the interaction asking where to save a file; the input is the suggested name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets the command saving the selected file.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SaveCommand { get; }

    /// <summary>Describes a file size in words, for example "12 KB".</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The description.</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        1 => "1 byte",
        < (long)Kilobyte => string.Create(CultureInfo.CurrentCulture, $"{bytes} bytes"),
        < (long)Megabyte => string.Create(CultureInfo.CurrentCulture, $"{bytes / Kilobyte:0} KB"),
        _ => string.Create(CultureInfo.CurrentCulture, $"{bytes / Megabyte:0.0} MB"),
    };

    /// <summary>Lists the document's files; called when the document loads.</summary>
    public void Refresh()
    {
        Items.Clear();
        Selected = null;
        if (_owner.TryGetDocument() is IAttachmentSource source)
        {
            foreach (var attachment in source.GetAttachments())
            {
                Items.Add(attachment);
            }
        }

        HasAttachments = Items.Count > 0;
        if (!HasAttachments && _owner.IsAttachmentsMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <summary>Saves an embedded file to a path.</summary>
    /// <param name="attachment">The file.</param>
    /// <param name="path">Where to save it.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    public bool Save(DocumentAttachment attachment, string path)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (_owner.TryGetDocument() is not IAttachmentSource source)
        {
            return false;
        }

        try
        {
            bool saved;
            using (var stream = File.Create(path))
            {
                saved = source.SaveAttachment(attachment.Index, stream);
            }

            _owner.Notice = saved ? $"Saved {attachment.Name}." : $"Could not read {attachment.Name} from the document.";
            return saved;
        }
        catch (IOException ex)
        {
            _owner.Notice = $"Could not save {attachment.Name}: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.Notice = $"Could not save {attachment.Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>Asks where to save the selected file, then saves it.</summary>
    /// <returns>A task.</returns>
    private async Task SaveAsync()
    {
        if (Selected is not { } attachment)
        {
            return;
        }

        var path = await SaveInteraction.Handle(Path.GetFileName(attachment.Name)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
        {
            _ = Save(attachment, path);
        }
    }
}
