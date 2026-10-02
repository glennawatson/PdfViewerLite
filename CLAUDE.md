# CLAUDE.md

Guidance for AI agents working in this repository.

## Build, test, run

```bash
dotnet build PdfViewerLite.slnx                       # warnings are errors
dotnet test --solution PdfViewerLite.slnx              # TUnit on Microsoft.Testing.Platform; TUnit args go after --
dotnet test --project tests/PdfViewerLite.Core.Tests/PdfViewerLite.Core.Tests.csproj -- --treenode-filter "/*/*/TileCacheTests/*"
dotnet run --project src/PdfViewerLite.App -- file.pdf
scripts/publish-linux.sh linux-x64                     # Native AOT; must stay free of trim/AOT warnings
dotnet run -c Release --project benchmarks/PdfViewerLite.Benchmarks -- --filter '*'
```

Headless UI tests save screenshots when `PDFVIEWERLITE_SCREENSHOTS` is set.

## Conventions

- ReactiveUI style: the `.editorconfig` is ReactiveUI's, with the file header changed to Glenn Watson. Package
  versions live only in `Directory.Packages.props`.
- StyleSharp, PerformanceSharp and SecuritySharp analyzers run with warnings as errors. Fix the code rather than
  suppressing; do not add `#pragma warning disable` or `[SuppressMessage]` without the maintainer's approval.
  Rules that come up often: XML docs on every member, `[DebuggerDisplay]` on public types, named constants instead
  of magic numbers, explicit enum values, no optional parameters, `[MethodImpl(AggressiveInlining)]` on trivial
  expression-bodied members, C# 14 extension blocks instead of classic extension methods, `in` for large structs.
- Everything shipped must be trim and AOT safe: no reflection, source-generated JSON, compiled XAML bindings,
  `RestService.ForGenerated` for Refit, and no `ReactiveWindow`/`WhenActivated` (they require unreferenced code).
- PDFium calls must hold `PdfiumLibrary.EnterScope()`. The render hot path must not allocate.
