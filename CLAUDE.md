# Project rules

PdfViewerLite is a local PDF viewer for Linux, Windows and macOS. Primary users are people with ADHD or autism. Improve low-vision access alongside those needs. Follow the [comfort rules](../docs/PdViewerLite/COMFORT.md). Use the [research](docs/research/README.md) to guide choices. Keep controls clear, stable and adjustable.

## Performance

- Low allocations and low execution time are core requirements. Back every code change with relevant BenchmarkDotNet measurements. Compare baseline and change under the same conditions. Prefer the lowest measured cost that preserves correctness and access.
- Do not judge performance from source code or subjective impressions. Overlapping timing error bars are inconclusive. Every feature needs a benchmark.
- Measure allocations with EventPipe and `tools/PdfViewerLite.AllocationAudit`. Never use `MemoryDiagnoser`. Explain every measured allocation in `benchmarks/allocations-explained.json`. The render hot path must not allocate.

## Implementation

- Prefer sealed records for data snapshots. Prefer readonly record structs for small values. Use classes for mutable state, services and owned lifetimes. Record collection equality follows the collection's own equality.
- Libraries target `net10.0;net11.0`. The app, UI tests and benchmarks target `net10.0`. Use `global.json`. Package versions belong in `Directory.Packages.props`.
- Shipped code must be trim and Native AOT safe. Use generated JSON, bindings and HTTP clients. Avoid reflection and activation APIs that require unreferenced code.
- XAML is layout and styling only. Views implement `IViewFor<T>`. Use theme resources and visible action labels.
- Views bind in the constructor inside `this.WhenActivated(disposables => ...)`. Use `OneWayBind` and `Bind` for view model to view, `BindCommand` for commands and `BindInteraction` or `RegisterHandler` for interactions. Use `WhenChanged` only to react to a change, not to copy a value into a control.
- View models derive from `ReactiveObject`. Use ReactiveUI.SourceGenerators: `[Reactive]` partial properties, `[ReactiveCommand]` methods, and ReactiveUI.Binding's `[ObservableAsProperty]` with `ToProperty` for derived values. Put side effects in `WhenChanged` subscriptions, not setters.
- Notifications use ReactiveUI.Primitives observables and `SubscribeSafe`. Own subscriptions with `MultipleDisposable`. Observe events with ReactiveUI.Primitives.ObservableEvents `Events()`; never subscribe with `+=`. Do not block on async.
- HTTP goes through Refit clients with generated JSON contexts.
- Hold `PdfiumLibrary.EnterScope()` for every PDFium call. Keep native resources owned and disposed.
- Follow the analyzers and existing C# style. Document members, name constants and use explicit enum values. Fix warnings; do not add suppressions. The existing tools-only SST1449 exception is authorised.
- Use .NET tools only. Use Roslyn for structural C# edits. Run single-file tools with `dotnet run --file`. Keep docs short. Keep research sources and limits. Add no attribution trailers or generated-by footers.

## Verification

```bash
dotnet build PdfViewerLite.slnx
dotnet test --solution PdfViewerLite.slnx
dotnet run -c Release --project benchmarks/PdfViewerLite.Benchmarks -- --filter '*RelevantBenchmark*'
scripts/audit-allocations.sh '*RelevantBenchmark*'
scripts/publish-linux.sh linux-x64
```

Warnings are failures. Verify all affected frameworks, saved output and user-visible behaviour. TUnit uses Microsoft.Testing.Platform; runner arguments follow `--`. With SDK 11, use absolute paths for `--project`. UI tests and benchmarks run headless. Set `PDFVIEWERLITE_SCREENSHOTS` to save UI frames.

Continue authorised goal work through verification. Treat feedback as work to fix. Report concrete blockers and missing dependencies. Follow the user's current commit and publishing instructions.
