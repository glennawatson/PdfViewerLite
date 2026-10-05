// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// The "Sign with Certificate" window: a certificate file (PKCS #12, .p12 or .pfx), its password, and an optional reason
/// and place. Signing checks the password straight away, so a mistake is shown in the window rather than later.
/// </summary>
[DebuggerDisplay("{CertificatePath}")]
public sealed partial class CertificateSignViewModel : ReactiveObject, IDisposable
{
    /// <summary>Raises <see cref="Answered"/> with <see langword="true"/>.</summary>
    private readonly Signal<RxVoid> _confirmed = new();

    /// <summary>Whether a certificate file is named, so signing can start.</summary>
    private readonly IObservable<bool> _canSign;

    /// <summary>The loaded certificate, once the password was right.</summary>
    private X509Certificate2? _certificate;

    /// <summary>Initializes a new instance of the <see cref="CertificateSignViewModel"/> class.</summary>
    /// <param name="certificatePath">The certificate used last time, or an empty string.</param>
    public CertificateSignViewModel(string certificatePath)
    {
        CertificatePath = certificatePath;
        _canSign = this.WhenChanged(static vm => vm.CertificatePath).Select(static path => !string.IsNullOrWhiteSpace(path));
        Answered = Signal.Merge(_confirmed.Select(static _ => true), CancelCommand);
    }

    /// <summary>Gets or sets the certificate file.</summary>
    [Reactive]
    public partial string CertificatePath { get; set; }

    /// <summary>Gets or sets the certificate's password.</summary>
    [Reactive]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets why the document is signed; optional.</summary>
    [Reactive]
    public partial string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets where it is signed; optional.</summary>
    [Reactive]
    public partial string Location { get; set; } = string.Empty;

    /// <summary>Gets what went wrong with the certificate, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? Error { get; private set; }

    /// <summary>Gets the interaction asking for a certificate file.</summary>
    public Interaction<RxVoid, string?> BrowseInteraction { get; } = new();

    /// <summary>Gets the answer, which closes the window: <see langword="true"/> to sign.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Hands over the loaded certificate; the caller disposes it.</summary>
    /// <returns>The certificate, or <see langword="null"/> when none was loaded.</returns>
    public X509Certificate2? TakeCertificate()
    {
        var certificate = _certificate;
        _certificate = null;
        return certificate;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _certificate?.Dispose();
        _confirmed.Dispose();
    }

    /// <summary>Closes the window without signing.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Loads the certificate with the password, confirming when it can sign.</summary>
    [ReactiveCommand(CanExecute = nameof(_canSign))]
    private void Sign()
    {
        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(CertificatePath, Password);
            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                Error = "This certificate has no private key, so it cannot sign.";
                return;
            }

            _certificate?.Dispose();
            _certificate = certificate;
            Error = null;
            _confirmed.OnNext(RxVoid.Default);
        }
        catch (CryptographicException)
        {
            Error = "The password is not right, or the file is not a certificate.";
        }
        catch (IOException ex)
        {
            Error = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Asks for a certificate file.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task BrowseAsync()
    {
        if (await BrowseInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is { Length: > 0 } path)
        {
            CertificatePath = path;
        }
    }
}
