# Performance

Low allocations and low execution time are project requirements. Changes need relevant BenchmarkDotNet measurements against a baseline. Overlapping timing intervals do not prove a speed difference.

UI tests and benchmarks run headless. Allocation checks use EventPipe and the allocation audit. Each measured library allocation needs a reason in `benchmarks/allocations-explained.json`. Rendering must not allocate. Signature import owns its pixel buffers. Layout creation owns page and row arrays.

Run the checks in [architecture](ARCHITECTURE.md). Keep raw logs, traces and comparison results with local build artifacts. Use measurements to choose changes. Do not infer performance from code style.
