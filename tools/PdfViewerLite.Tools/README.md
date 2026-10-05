# Repository tools

Build once with the SDK pinned in `global.json`:

```bash
dotnet publish tools/PdfViewerLite.Tools -c Release --self-contained false -o artifacts/tools
dotnet artifacts/tools/PdfViewerLite.Tools.dll --help
```

Use `publish`, `package`, `icons`, `release-notes`, `ci`, `accessibility` or
`voice`. Each command has its own `--help`.

The release workflow connects SimplySign once. `sign authenticode` signs and
verifies Windows executables and installers using jsign. `sign detached` signs
and verifies archive signatures using .NET CMS APIs. Apple signing stays in the
macOS packaging command. All Certum signing runs in one Linux job.

Dependency versions are in `tools/Directory.Packages.props`. See
[the dependency audit](../DEPENDENCIES.md) for the retained packages and native
signing bridge.
