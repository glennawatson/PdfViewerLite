// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using PdfViewerLite.Core.Signatures;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// A tab's digital signatures. Signatures are only checked when the user asks, because checking trust may look up
/// certificate revocation online; the check runs off the UI thread.
/// </summary>
[DebuggerDisplay("{SignatureCount} signatures")]
public sealed class SignaturesViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="SignaturesViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public SignaturesViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync);
    }

    /// <summary>Gets the interaction asking the view to show the checked signatures.</summary>
    public Interaction<SignaturesViewModel, RxVoid> ShowInteraction { get; } = new();

    /// <summary>Gets the checked signatures.</summary>
    public ObservableCollection<DocumentSignature> Signatures { get; } = [];

    /// <summary>Gets the number of signatures in the document.</summary>
    public int SignatureCount
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether a check is running.</summary>
    public bool IsChecking
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the document's file name, for the window title.</summary>
    public string FileName => _owner.FileName;

    /// <summary>Gets the command checking every signature and showing the result.</summary>
    public ReactiveCommand<RxVoid, RxVoid> CheckCommand { get; }

    /// <summary>Counts the document's signatures; called when the document loads.</summary>
    public void Refresh()
    {
        SignatureCount = (_owner.TryGetDocument() as ISignatureSource)?.SignatureCount ?? 0;
        Signatures.Clear();
    }

    /// <summary>Checks every signature against the file.</summary>
    /// <param name="raw">The signatures as stored.</param>
    /// <param name="path">The file.</param>
    /// <returns>The checked signatures.</returns>
    private static DocumentSignature[] VerifyAll(IReadOnlyList<RawSignature> raw, string path)
    {
        var results = new DocumentSignature[raw.Count];
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = SignatureVerifier.Verify(raw[i], path, []);
        }

        return results;
    }

    /// <summary>Checks every signature off the UI thread, then shows the results.</summary>
    /// <returns>A task.</returns>
    private async Task CheckAsync()
    {
        if (_owner.TryGetDocument() is not ISignatureSource source)
        {
            return;
        }

        IsChecking = true;
        try
        {
            var raw = source.GetSignatures();
            var path = _owner.FilePath;
            var checkedSignatures = await Task.Run(() => VerifyAll(raw, path)).ConfigureAwait(true);
            Signatures.Clear();
            foreach (var signature in checkedSignatures)
            {
                Signatures.Add(signature);
            }
        }
        finally
        {
            IsChecking = false;
        }

        _ = await ShowInteraction.Handle(this).ToTask().ConfigureAwait(true);
    }
}
