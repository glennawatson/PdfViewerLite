// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.Core.Signatures.Signing;
using PdfViewerLite.Http.Signatures;
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

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>Initializes a new instance of the <see cref="SignaturesViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public SignaturesViewModel(DocumentTabViewModel owner, AppServices services)
    {
        _owner = owner;
        _services = services;
        CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync);
        SignWithCertificateCommand = ReactiveCommand.CreateFromTask(SignWithCertificateAsync);
    }

    /// <summary>Gets the interaction showing the "Sign with Certificate" window; the output says whether to sign.</summary>
    public Interaction<CertificateSignViewModel, bool> CertificateSignInteraction { get; } = new();

    /// <summary>Gets the command signing a copy of the document with a certificate.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SignWithCertificateCommand { get; }

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

    /// <summary>Determines whether an exception is a signing failure to tell the user about, rather than a bug.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for a damaged file, a certificate problem, a file error or an unreachable timestamp server.</returns>
    private static bool IsSigningFailure(Exception exception) =>
        exception is InvalidDataException or NotSupportedException or CryptographicException or IOException or UnauthorizedAccessException
            or HttpRequestException or TaskCanceledException;

    /// <summary>Checks every signature against the file.</summary>
    /// <param name="raw">The signatures as stored.</param>
    /// <param name="path">The file.</param>
    /// <returns>The checked signatures.</returns>
    private static DocumentSignature[] VerifyAll(IReadOnlyList<RawSignature> raw, string path)
    {
        var results = new DocumentSignature[raw.Count];
        var store = DocumentSecurityStore.Read(File.ReadAllBytes(path));
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = SignatureVerifier.Verify(raw[i], path, [], store);
        }

        return results;
    }

    /// <summary>Gets the document as it is now: its saved bytes, or with unsaved edits written in.</summary>
    /// <returns>The bytes, or <see langword="null"/> when the document is not open.</returns>
    private byte[]? ReadCurrent()
    {
        var document = _owner.TryGetDocument();
        if (document is IAnnotationEditor { HasUnsavedChanges: true } editor)
        {
            using var stream = new MemoryStream();
            return editor.Save(stream) ? stream.ToArray() : null;
        }

        return document is null ? null : File.ReadAllBytes(_owner.FilePath);
    }

    /// <summary>Asks for the certificate and where to save, signs a copy off the UI thread and opens it.</summary>
    /// <returns>A task.</returns>
    private async Task SignWithCertificateAsync()
    {
        using var request = new CertificateSignViewModel(_services.Settings.SigningCertificatePath);
        if (!await CertificateSignInteraction.Handle(request).ToTask().ConfigureAwait(true) || request.TakeCertificate() is not { } certificate)
        {
            return;
        }

        using (certificate)
        {
            _services.Settings.SigningCertificatePath = request.CertificatePath;
            _services.SaveSettings();
            var suggested = $"{Path.GetFileNameWithoutExtension(_owner.FileName)}-signed.pdf";
            var destination = await _owner.SaveAsInteraction.Handle(suggested).ToTask().ConfigureAwait(true);
            if (string.IsNullOrEmpty(destination) || ReadCurrent() is not { } source)
            {
                return;
            }

            var signing = new SigningRequest(Math.Max(0, _owner.CurrentPageIndex), request.Reason, request.Location, TimeProvider.System.GetUtcNow());
            try
            {
                var timestamper = Uri.TryCreate(_services.Settings.TimestampServer, UriKind.Absolute, out var server) ? new TimestampAuthorityClient(server) : null;
                var signed = await Task.Run(() => PdfSigner.Sign(source, certificate, signing, timestamper)).ConfigureAwait(true);
                var temporary = $"{destination}.signing";
                await File.WriteAllBytesAsync(temporary, signed).ConfigureAwait(true);
                FileReplacement.Replace(temporary, destination);
            }
            catch (Exception ex) when (IsSigningFailure(ex))
            {
                _owner.Notice = $"Could not sign: {ex.Message}";
                return;
            }

            _owner.Notice = $"Signed copy saved as {Path.GetFileName(destination)}.";
            _services.RequestOpen(destination);
        }
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
