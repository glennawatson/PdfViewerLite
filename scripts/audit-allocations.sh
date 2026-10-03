#!/usr/bin/env bash
# Runs the benchmarks with allocation tracing and fails when PdfViewerLite allocates anything not listed in
# benchmarks/allocations-explained.json. Pass a BenchmarkDotNet filter, for example '*TileRender*'.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FILTER="${1:-*}"
ARTIFACTS="$ROOT/benchmarks/PdfViewerLite.Benchmarks/BenchmarkDotNet.Artifacts"
rm -rf "$ARTIFACTS"
dotnet run -c Release --project "$ROOT/benchmarks/PdfViewerLite.Benchmarks" -- --filter "$FILTER" --audit --job short --artifacts "$ARTIFACTS"
dotnet run -c Release --project "$ROOT/tools/PdfViewerLite.AllocationAudit" -- "$ARTIFACTS" "$ROOT/benchmarks/allocations-explained.json"
