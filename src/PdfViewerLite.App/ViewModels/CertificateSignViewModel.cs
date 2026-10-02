// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// The "Sign with Certificate" window: a certificate file (PKCS #12, .p12 or .pfx), its password, and an optional reason
/// and place. Signing checks the password straight away, so a mistake is shown in the window rather than later.
/// </summary>
[DebuggerDisplay("{CertificatePath}")]
public sealed class CertificateSignViewModel : ReactiveObject, IDisposable
{
    /// <summary>Raises <see cref="Confirmed"/>.</summary>
    private readonly Signal<RxVoid> _confirmed = new();

    /// <summary>The loaded certificate, once the password was right.</summary>
    private X509Certificate2? _certificate;

    /// <summary>Initializes a new instance of the <see cref="CertificateSignViewModel"/> class.</summary>
    /// <param name="certificatePath">The certificate used last time, or an empty string.</param>
    public CertificateSignViewModel(string certificatePath)
    {
        CertificatePath = certificatePath;
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseAsync);
        SignCommand = ReactiveCommand.Create(Sign, this.WhenAnyValue(static vm => vm.CertificatePath).Select(static path => !string.IsNullOrWhiteSpace(path)));
    }

    /// <summary>Gets or sets the certificate file.</summary>
    public string CertificatePath
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the certificate's password.</summary>
    public string Password
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets why the document is signed; optional.</summary>
    public string Reason
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets where it is signed; optional.</summary>
    public string Location
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets what went wrong with the certificate, or <see langword="null"/>.</summary>
    public string? Error
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the interaction asking for a certificate file.</summary>
    public Interaction<RxVoid, string?> BrowseInteraction { get; } = new();

    /// <summary>Gets the command choosing a certificate file.</summary>
    public ReactiveCommand<RxVoid, RxVoid> BrowseCommand { get; }

    /// <summary>Gets the command checking the certificate and confirming.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SignCommand { get; }

    /// <summary>Gets the confirmations, which close the window.</summary>
    public IObservable<RxVoid> Confirmed => _confirmed;

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

    /// <summary>Loads the certificate with the password, confirming when it can sign.</summary>
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
    private async Task BrowseAsync()
    {
        if (await BrowseInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is { Length: > 0 } path)
        {
            CertificatePath = path;
        }
    }
}
