# Tools dependencies

The tools app uses public .NET APIs for ZIP, TAR, GZip, DEFLATE, XML, JSON,
hashing, X.509 certificates and CMS signatures. Package versions belong in
`tools/Directory.Packages.props`, which imports the root package file.

| Dependency | Decision | Reason |
| --- | --- | --- |
| System.CommandLine | Keep | Requested command parser. Microsoft package. |
| Refit | Keep | Project HTTP tooling; retained by request. Uses generated clients. |
| SkiaSharp and its native assets | Keep | Cross-platform PNG decoding and icon scaling; retained by request. |
| OpenMcdf | Keep | Updates MSI compound-file streams on Linux; retained by request. .NET has no cross-platform compound-file storage API. |
| System.Security.Cryptography.Pkcs | Keep | Microsoft's CMS implementation. X.509 alone does not encode or verify CMS signatures. |
| SharpCompress | Remove | MSIX now uses .NET 11 `DeflateEncoder` and the C# ZIP writer. |
| Pkcs11Interop.X509Store and Pkcs11Interop | Remove | .NET's `SafeEvpPKeyHandle.OpenKeyFromProvider` opens the token key; `RSAOpenSsl` and `SignedCms` sign the file. |
| System.Drawing.Common | Remove | Icon generation uses the retained SkiaSharp dependency. |
| ONNX Runtime, through the Speech project | Keep | The Melo check runs actual model inference. .NET does not include an ONNX inference engine. |
| ReactiveUI.Primitives, through the Core project | Keep | Required by the shared application types used through Speech. No separate tools reference is added. |
| TraceEvent, in AllocationAudit | Keep | Reads EventPipe traces. .NET can collect diagnostic events, but does not provide an equivalent public EventPipe file reader. |
| MinVer and the three analyzers | Keep | Existing build tooling and project checks. These are private build dependencies. |

## Signing bridge

.NET's Linux X.509 store does not expose the SimplySign PKCS#11 key directly.
The public .NET key-provider API still needs a native OpenSSL PKCS#11 provider
to reach the existing SimplySign module. This replaces the two managed PKCS#11
packages; it does not remove the native bridge.

Before launching the signing app, the signing driver must set:

- `PKCS11_PROVIDER`: installed `pkcs11prov.so` path.
- `PKCS11_MODULE_PATH`: the connected SimplySign module, from `SS_PKCS11`.
- `PKCS11_PIN`: the connected session's PIN, when required.
- `PKCS11_KEY_URI`: optional key selection; defaults to `pkcs11:type=private`.

Set these in the parent process environment. Native libraries must see the same
settings as the .NET process. Keep credentials out of command-line logs.

The public signing certificate comes from an independently verified executable
already signed by jsign. Its SHA-256 hash must match the release certificate pin.
The detached signature is verified with that same pin after signing. The private
key stays with SimplySign.

## Verification

The software-token probe passed .NET CMS signing, certificate-pin rejection and
tamper rejection. This does not verify the live SimplySign service.

The managed MSI was independently extracted and its executables matched the
signed payloads. The managed MSIX passed 1,350 independent block decompressions,
SHA-256 checks and compressed byte-count checks. jsign signed both packages and
the independent native verifier accepted both signatures.

The solution builds without warnings. The tests pass, with corpus, voice model
and platform-dependent checks skipped where their fixtures are unavailable.
The shared actions pass compilation, contract and workflow checks. Live
SimplySign and Windows/macOS execution require their GitHub release runners.
